using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Chat.Commands;

/// <summary>
/// Xử lý đánh dấu đã đọc tin nhắn trong cuộc hội thoại và cập nhật lại bộ đếm UnreadCount.
/// </summary>
public class MarkConversationReadCommandHandler : IRequestHandler<MarkConversationReadCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeService _chatRealtimeService;

    public MarkConversationReadCommandHandler(
        IUnitOfWork unitOfWork,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<int> Handle(MarkConversationReadCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại với ID '{request.ConversationId}'.");

        if (!conversation.CanUserAccess(request.ReaderUserId, request.ReaderRole))
        {
            throw new ForbiddenAccessException(
                "Cuộc hội thoại này là riêng tư hoặc đã được một nhân viên/quản trị viên khác tiếp nhận xử lý. Bạn không có quyền truy cập.");
        }

        // Nếu là cuộc hội thoại SUPPORT đang PENDING mà nhân viên chỉ mới xem hàng đợi chưa bấm Duyệt thì không đánh dấu đã đọc của khách
        if (conversation.Type == ConversationType.SUPPORT &&
            conversation.AssignedStaffId == null &&
            request.ReaderUserId != conversation.InitiatorUserId)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        var markedCount = await _unitOfWork.ChatMessages.MarkMessagesAsReadAsync(
            request.ConversationId,
            request.ReaderUserId,
            now,
            cancellationToken);

        if (request.ReaderUserId == conversation.InitiatorUserId)
        {
            conversation.UnreadCountInitiator = 0;
        }
        else
        {
            conversation.UnreadCountTarget = 0;
        }

        conversation.UpdatedDate = now;
        _unitOfWork.ChatConversations.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        Guid? otherPartyId;
        if (request.ReaderUserId == conversation.InitiatorUserId)
        {
            otherPartyId = conversation.Type == ConversationType.DIRECT
                ? conversation.ParticipantUserId
                : conversation.AssignedStaffId;
        }
        else
        {
            otherPartyId = conversation.InitiatorUserId;
        }

        await _chatRealtimeService.NotifyMessagesReadAsync(
            conversation.Id,
            request.ReaderUserId,
            otherPartyId,
            now,
            cancellationToken);

        return markedCount;
    }
}
