using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller quản lý chức năng Chat trực tuyến:
/// 1. Hội thoại 1-1 giữa những người dùng đã xác thực danh tính (Direct Chat).
/// 2. Hội thoại giữa người dùng với Nhân viên Sàn (Support Chat - riêng tư tuyệt đối sau khi nhân viên/admin duyệt).
/// </summary>
[Authorize]
public class ChatController : BaseApiController
{
    private UserRole ResolveCurrentUserRole()
    {
        if (IsAdmin) return UserRole.ADMIN;
        if (IsConsultant) return UserRole.CONSULTANT;
        return UserRole.USER;
    }

    /// <summary>
    /// Tìm kiếm người dùng khác thông qua Email hoặc Số điện thoại liên lạc trong UserProfile.
    /// Yêu cầu cả người tìm kiếm và người được tìm thấy đều đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC).
    /// </summary>
    [HttpGet("users/search")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ChatUserSearchDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SearchVerifiedUsers([FromQuery] string keyword, [FromQuery] int limit = 10)
    {
        var query = new SearchVerifiedUsersForChatQuery(
            Keyword: keyword ?? string.Empty,
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole(),
            Limit: limit);

        var result = await Mediator.Send(query);
        return HandleResult(result, "Tìm kiếm người dùng thành công.");
    }

    /// <summary>
    /// Lấy danh sách các cuộc hội thoại của người dùng hiện tại (hỗ trợ lọc theo loại DIRECT/SUPPORT và trạng thái).
    /// Với hội thoại SUPPORT đã được một Nhân viên Sàn hoặc Admin duyệt nhận, các Nhân viên Sàn và Admin khác sẽ không thể nhìn thấy.
    /// </summary>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ChatConversationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConversations(
        [FromQuery] ConversationType? type = null,
        [FromQuery] SupportConversationStatus? supportStatus = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetUserConversationsQuery(
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole(),
            Type: type,
            SupportStatus: supportStatus,
            PageIndex: pageIndex,
            PageSize: pageSize);

        var result = await Mediator.Send(query);
        return HandlePagedResult(result, "Lấy danh sách cuộc hội thoại thành công.");
    }

    /// <summary>
    /// Mở mới hoặc lấy lại cuộc hội thoại trực tiếp 1-1 (DIRECT) với một người dùng đã xác thực danh tính khác.
    /// </summary>
    [HttpPost("conversations/direct")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrCreateDirectConversation([FromBody] CreateDirectConversationRequest request)
    {
        var command = new GetOrCreateDirectConversationCommand(
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole(),
            TargetUserId: request.TargetUserId,
            InitialMessage: request.InitialMessage);

        var result = await Mediator.Send(command);
        return HandleResult(result, "Mở cuộc hội thoại trực tiếp thành công.");
    }

    /// <summary>
    /// Mở mới hoặc lấy lại cuộc hội thoại hỗ trợ với Sàn TrustSim (SUPPORT).
    /// Nếu phiên hỗ trợ trước đó đã được Sàn duyệt (CLAIMED), khi bấm nút "Chat với sàn" hệ thống sẽ đóng phiên cũ (RESOLVED)
    /// và tạo một đoạn chat mới ở trạng thái PENDING để được duyệt lại (có thể được duyệt bởi nhân viên/admin khác).
    /// </summary>
    [HttpPost("conversations/support")]
    [Authorize(Roles = "USER,User")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOrCreateSupportConversation([FromBody] CreateSupportConversationRequest? request)
    {
        var command = new GetOrCreateSupportConversationCommand(
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole(),
            InitialMessage: request?.InitialMessage);

        var result = await Mediator.Send(command);
        return HandleResult(result, "Mở cuộc hội thoại hỗ trợ với Sàn thành công.");
    }

    /// <summary>
    /// [Nhân viên Sàn / Admin] Bấm Chấp nhận / Duyệt (Claim) một cuộc hội thoại hỗ trợ đang chờ (PENDING).
    /// Sau khi duyệt, cuộc hội thoại lập tức trở thành RIÊNG TƯ giữa nhân viên/admin đó và người dùng; mọi nhân viên/admin khác không thể nhìn thấy và không thể can thiệp nữa.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/claim")]
    [Authorize(Roles = "ADMIN,Admin,CONSULTANT,Consultant")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ClaimSupportConversation([FromRoute] Guid conversationId)
    {
        var command = new ClaimSupportConversationCommand(
            ConversationId: conversationId,
            StaffUserId: CurrentUserId,
            StaffRole: ResolveCurrentUserRole());

        var result = await Mediator.Send(command);
        return HandleResult(result, "Đã tiếp nhận cuộc hội thoại hỗ trợ. Cuộc hội thoại hiện được bảo mật riêng tư.");
    }

    /// <summary>
    /// Kết thúc / đóng một phiên hội thoại hỗ trợ với Sàn (chuyển trạng thái sang RESOLVED).
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<ChatConversationSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveSupportConversation([FromRoute] Guid conversationId)
    {
        var command = new ResolveSupportConversationCommand(
            ConversationId: conversationId,
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole());

        var result = await Mediator.Send(command);
        return HandleResult(result, "Đã kết thúc phiên hội thoại hỗ trợ.");
    }

    /// <summary>
    /// Lấy danh sách lịch sử tin nhắn trong một cuộc hội thoại (phân trang).
    /// Chặn 403 Forbidden nếu không phải thành viên hoặc nếu cuộc hội thoại Support đã được nhân viên/admin khác duyệt nhận.
    /// </summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ChatMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConversationMessages(
        [FromRoute] Guid conversationId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new GetConversationMessagesQuery(
            ConversationId: conversationId,
            CurrentUserId: CurrentUserId,
            CurrentUserRole: ResolveCurrentUserRole(),
            PageIndex: pageIndex,
            PageSize: pageSize);

        var result = await Mediator.Send(query);
        return HandlePagedResult(result, "Lấy lịch sử tin nhắn thành công.");
    }

    /// <summary>
    /// Gửi tin nhắn mới (văn bản hoặc đính kèm tệp Hình ảnh / PDF đạt chuẩn Font chữ đa thiết bị) vào cuộc hội thoại.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/messages")]
    [Consumes("multipart/form-data", "application/json")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendMessage(
        [FromRoute] Guid conversationId,
        [FromForm] SendChatMessageRequest request)
    {
        var command = new SendChatMessageCommand(
            ConversationId: conversationId,
            SenderId: CurrentUserId,
            SenderRole: ResolveCurrentUserRole(),
            Content: request.Content,
            AttachmentFile: request.AttachmentFile);

        var result = await Mediator.Send(command);
        return HandleResult(result, "Gửi tin nhắn thành công.");
    }

    /// <summary>
    /// Đánh dấu đã đọc toàn bộ tin nhắn trong cuộc hội thoại.
    /// </summary>
    [HttpPost("conversations/{conversationId:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> MarkAsRead([FromRoute] Guid conversationId)
    {
        var command = new MarkConversationReadCommand(
            ConversationId: conversationId,
            ReaderUserId: CurrentUserId,
            ReaderRole: ResolveCurrentUserRole());

        var count = await Mediator.Send(command);
        return HandleResult(count, "Đã đánh dấu đọc tin nhắn.");
    }
}
