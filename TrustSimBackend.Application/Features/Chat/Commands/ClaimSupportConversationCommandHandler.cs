using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Application.Features.Chat.Helpers;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Chat.Commands;

/// <summary>
/// Xử lý việc Nhân viên Sàn (CONSULTANT) hoặc ADMIN bấm Duyệt / Chấp nhận hỗ trợ một cuộc hội thoại đang PENDING.
/// Đảm bảo tính nguyên tử (Atomic Claim chống Race Condition) và lập tức chuyển cuộc hội thoại thành RIÊNG TƯ tuyệt đối.
/// </summary>
public class ClaimSupportConversationCommandHandler : IRequestHandler<ClaimSupportConversationCommand, ChatConversationSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeService _chatRealtimeService;

    public ClaimSupportConversationCommandHandler(
        IUnitOfWork unitOfWork,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<ChatConversationSummaryDto> Handle(ClaimSupportConversationCommand request, CancellationToken cancellationToken)
    {
        if (request.StaffRole != UserRole.ADMIN && request.StaffRole != UserRole.CONSULTANT)
        {
            throw new ForbiddenAccessException("Chỉ Nhân viên Sàn hoặc Quản trị viên mới có quyền duyệt tiếp nhận cuộc hội thoại hỗ trợ.");
        }

        var staffUser = await _unitOfWork.Users.GetByIdAsync(request.StaffUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản nhân viên với ID '{request.StaffUserId}'.");

        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại hỗ trợ với ID '{request.ConversationId}'.");

        if (conversation.Type != ConversationType.SUPPORT)
        {
            throw new BadRequestException("Chỉ có thể tiếp nhận xử lý đối với cuộc hội thoại hỗ trợ Sàn (SUPPORT).");
        }

        // Chặn tuyệt đối tự duyệt: Nhân viên hoặc Admin không được phép duyệt cuộc hội thoại do chính mình tạo ra
        if (conversation.InitiatorUserId == request.StaffUserId)
        {
            throw new ForbiddenAccessException(
                "Nhân viên hoặc Quản trị viên không thể tự duyệt cuộc hội thoại hỗ trợ do chính mình tạo ra.");
        }

        // Nếu cuộc hội thoại đã được người khác nhận trước đó -> Chặn tuyệt đối
        if (conversation.AssignedStaffId.HasValue)
        {
            if (conversation.AssignedStaffId.Value == request.StaffUserId)
            {
                return ChatMappingHelper.MapConversationSummary(conversation, request.StaffUserId, request.StaffRole);
            }

            throw new ForbiddenAccessException(
                "Cuộc hội thoại hỗ trợ này đã được một nhân viên hoặc quản trị viên khác tiếp nhận riêng tư. Bạn không có quyền nhìn thấy hoặc can thiệp.");
        }

        var now = DateTime.UtcNow;

        // Thực hiện Atomic Claim chống Race Condition khi 2 nhân viên bấm duyệt cùng một thời điểm
        var claimedSuccess = await _unitOfWork.ChatConversations.TryClaimSupportConversationAtomicallyAsync(
            request.ConversationId,
            request.StaffUserId,
            now,
            cancellationToken);

        if (!claimedSuccess)
        {
            throw new ForbiddenAccessException(
                "Cuộc hội thoại này vừa được một nhân viên hoặc quản trị viên khác tiếp nhận xử lý.");
        }

        _unitOfWork.ClearChangeTracker();

        var updatedConversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại hỗ trợ với ID '{request.ConversationId}'.");

        var staffFullName = !string.IsNullOrWhiteSpace(staffUser.UserProfile?.FullName)
            ? staffUser.UserProfile.FullName
            : (staffUser.UserName ?? "Chuyên viên Sàn");

        var systemMsgContent = $"Chuyên viên {staffFullName} đã tiếp nhận hỗ trợ cuộc hội thoại này.";
        var systemMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = updatedConversation.Id,
            SenderId = request.StaffUserId,
            MessageType = ChatMessageType.SYSTEM,
            Content = systemMsgContent,
            IsRead = false,
            CreatedDate = now,
            UpdatedDate = now
        };

        await _unitOfWork.ChatMessages.AddAsync(systemMessage, cancellationToken);

        updatedConversation.LastMessagePreview = systemMsgContent;
        updatedConversation.LastMessageAt = now;
        updatedConversation.LastMessageSenderId = request.StaffUserId;
        updatedConversation.UnreadCountInitiator += 1;
        updatedConversation.UpdatedDate = now;

        _unitOfWork.ChatConversations.Update(updatedConversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        systemMessage.Sender = updatedConversation.AssignedStaff ?? staffUser;

        var summaryForInitiator = ChatMappingHelper.MapConversationSummary(
            updatedConversation,
            updatedConversation.InitiatorUserId,
            UserRole.USER);

        var summaryForClaimedStaff = ChatMappingHelper.MapConversationSummary(
            updatedConversation,
            request.StaffUserId,
            request.StaffRole);

        var systemMessageDto = ChatMappingHelper.MapMessage(systemMessage);

        await _chatRealtimeService.NotifySupportConversationClaimedAsync(
            updatedConversation.Id,
            updatedConversation.InitiatorUserId,
            request.StaffUserId,
            staffFullName,
            now,
            summaryForInitiator,
            summaryForClaimedStaff,
            systemMessageDto,
            cancellationToken);

        return summaryForClaimedStaff;
    }
}
