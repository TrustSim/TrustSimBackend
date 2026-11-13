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
/// Xử lý kết thúc / đóng phiên hội thoại hỗ trợ với Sàn (chuyển trạng thái sang RESOLVED).
/// Chỉ Khách hàng khởi tạo hoặc Nhân viên/Admin đã tiếp nhận phiên hỗ trợ đó mới có quyền đóng.
/// </summary>
public class ResolveSupportConversationCommandHandler : IRequestHandler<ResolveSupportConversationCommand, ChatConversationSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeService _chatRealtimeService;

    public ResolveSupportConversationCommandHandler(
        IUnitOfWork unitOfWork,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<ChatConversationSummaryDto> Handle(ResolveSupportConversationCommand request, CancellationToken cancellationToken)
    {
        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại hỗ trợ với ID '{request.ConversationId}'.");

        if (conversation.Type != ConversationType.SUPPORT)
        {
            throw new BadRequestException("Chỉ có thể kết thúc phiên đối với cuộc hội thoại hỗ trợ Sàn (SUPPORT).");
        }

        if (!conversation.CanUserAccess(request.CurrentUserId, request.CurrentUserRole))
        {
            throw new ForbiddenAccessException(
                "Cuộc hội thoại hỗ trợ này thuộc quyền riêng tư của nhân viên khác. Bạn không có quyền kết thúc hoặc can thiệp.");
        }

        if (conversation.SupportStatus == SupportConversationStatus.RESOLVED)
        {
            return ChatMappingHelper.MapConversationSummary(conversation, request.CurrentUserId, request.CurrentUserRole);
        }

        var now = DateTime.UtcNow;
        conversation.SupportStatus = SupportConversationStatus.RESOLVED;
        conversation.UpdatedDate = now;

        var actorName = request.CurrentUserId == conversation.InitiatorUserId
            ? (conversation.InitiatorUser?.UserProfile?.FullName ?? "Khách hàng")
            : (conversation.AssignedStaff?.UserProfile?.FullName ?? "Chuyên viên Sàn");

        var systemMsgContent = $"{actorName} đã kết thúc phiên hỗ trợ này.";
        var systemMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = request.CurrentUserId,
            MessageType = ChatMessageType.SYSTEM,
            Content = systemMsgContent,
            IsRead = false,
            CreatedDate = now,
            UpdatedDate = now
        };

        await _unitOfWork.ChatMessages.AddAsync(systemMessage, cancellationToken);

        conversation.LastMessagePreview = systemMsgContent;
        conversation.LastMessageAt = now;
        conversation.LastMessageSenderId = request.CurrentUserId;

        _unitOfWork.ChatConversations.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var summaryForInitiator = ChatMappingHelper.MapConversationSummary(
            conversation,
            conversation.InitiatorUserId,
            UserRole.USER);

        var summaryForStaff = ChatMappingHelper.MapConversationSummary(
            conversation,
            conversation.AssignedStaffId ?? Guid.Empty,
            conversation.AssignedStaff?.Role ?? UserRole.CONSULTANT);

        var systemMessageDto = ChatMappingHelper.MapMessage(systemMessage);

        await _chatRealtimeService.NotifyNewMessageAsync(
            summaryForInitiator,
            summaryForStaff,
            systemMessageDto,
            conversation.InitiatorUserId,
            conversation.AssignedStaffId,
            broadcastToSupportQueue: false,
            cancellationToken);

        return ChatMappingHelper.MapConversationSummary(conversation, request.CurrentUserId, request.CurrentUserRole);
    }
}
