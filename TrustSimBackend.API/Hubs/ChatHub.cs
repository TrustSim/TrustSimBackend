using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.API.Hubs;

/// <summary>
/// SignalR Hub điều phối kết nối WebSocket thời gian thực cho hệ thống Chat (User-to-User &amp; User-to-Support).
/// Tự động định tuyến hòm thư cá nhân theo JWT Identity và kiểm soát nghiêm ngặt quyền riêng tư phòng chat.
/// </summary>
[Authorize]
public class ChatHub : Hub<IChatHubClient>
{
    public const string SupportQueueGroupName = "support_queue";

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IUnitOfWork unitOfWork, ILogger<ChatHub> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public static string GetUserGroupName(Guid userId) => $"chat_user_{userId.ToString().ToLowerInvariant()}";
    public static string GetConversationGroupName(Guid conversationId) => $"chat_conv_{conversationId.ToString().ToLowerInvariant()}";

    public override async Task OnConnectedAsync()
    {
        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();

        if (userId != Guid.Empty)
        {
            var userGroup = GetUserGroupName(userId);
            await Groups.AddToGroupAsync(Context.ConnectionId, userGroup);

            if (userRole == UserRole.ADMIN || userRole == UserRole.CONSULTANT)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, SupportQueueGroupName);
                _logger.LogInformation(
                    "[ChatHub] Nhân viên/Admin {UserId} ({Role}) đã tham gia hàng đợi {SupportQueue}",
                    userId, userRole, SupportQueueGroupName);
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetCurrentUserId();
        if (userId != Guid.Empty)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetUserGroupName(userId));
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, SupportQueueGroupName);
        }

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Tham gia phòng theo dõi trực tiếp một cuộc hội thoại cụ thể (sau khi kiểm tra quyền riêng tư CanUserAccess).
    /// </summary>
    public async Task JoinConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var convGuid))
        {
            return;
        }

        var userId = GetCurrentUserId();
        var userRole = GetCurrentUserRole();
        if (userId == Guid.Empty)
        {
            throw new HubException("Yêu cầu đăng nhập để tham gia cuộc hội thoại.");
        }

        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(convGuid);
        if (conversation == null || !conversation.CanUserAccess(userId, userRole))
        {
            throw new HubException("Cuộc hội thoại này là riêng tư hoặc đã được nhân viên khác tiếp nhận. Bạn không có quyền tham gia.");
        }

        var groupName = GetConversationGroupName(convGuid);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Rời phòng theo dõi cuộc hội thoại.
    /// </summary>
    public async Task LeaveConversation(string conversationId)
    {
        if (!Guid.TryParse(conversationId, out var convGuid))
        {
            return;
        }

        var groupName = GetConversationGroupName(convGuid);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
    }

    private Guid GetCurrentUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? Context.User?.FindFirst("nameid")?.Value
                 ?? Context.User?.FindFirst("sub")?.Value;

        return Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private UserRole GetCurrentUserRole()
    {
        var roleStr = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                   ?? Context.User?.FindFirst("role")?.Value;

        if (!string.IsNullOrWhiteSpace(roleStr) && Enum.TryParse<UserRole>(roleStr, ignoreCase: true, out var role))
        {
            return role;
        }

        return UserRole.USER;
    }
}
