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
/// Xử lý mở mới hoặc lấy lại cuộc hội thoại giữa người dùng với Nhân viên Sàn TrustSim (SUPPORT).
/// Khi chưa có nhân viên tiếp nhận (PENDING), mọi Nhân viên Sàn (CONSULTANT) và ADMIN đều nhìn thấy và có thể bấm Duyệt.
/// </summary>
public class GetOrCreateSupportConversationCommandHandler : IRequestHandler<GetOrCreateSupportConversationCommand, ChatConversationSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeService _chatRealtimeService;

    public GetOrCreateSupportConversationCommandHandler(
        IUnitOfWork unitOfWork,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<ChatConversationSummaryDto> Handle(GetOrCreateSupportConversationCommand request, CancellationToken cancellationToken)
    {
        var currentUser = await _unitOfWork.Users.GetByIdAsync(request.CurrentUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.CurrentUserId}'.");

        if (!currentUser.IsActive || currentUser.IsDeleted)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động.");
        }

        if (currentUser.UserProfile == null || currentUser.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new UserProfileNotVerifiedException(
                "Chỉ những người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC) mới được phép nhắn tin hỗ trợ.");
        }

        var conversation = await _unitOfWork.ChatConversations.GetActiveSupportConversationByUserIdAsync(
            request.CurrentUserId,
            cancellationToken);

        var now = DateTime.UtcNow;
        bool isNewlyCreated = false;

        if (conversation == null)
        {
            isNewlyCreated = true;
            conversation = new ChatConversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.SUPPORT,
                InitiatorUserId = request.CurrentUserId,
                ParticipantUserId = null,
                AssignedStaffId = null,
                SupportStatus = SupportConversationStatus.PENDING,
                CreatedDate = now,
                UpdatedDate = now
            };

            await _unitOfWork.ChatConversations.AddAsync(conversation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(conversation.Id, cancellationToken)
                ?? conversation;
        }

        if (!string.IsNullOrWhiteSpace(request.InitialMessage))
        {
            var trimmedContent = request.InitialMessage.Trim();
            var preview = trimmedContent.Length > 450 ? trimmedContent[..450] + "..." : trimmedContent;

            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversation.Id,
                SenderId = request.CurrentUserId,
                MessageType = ChatMessageType.TEXT,
                Content = trimmedContent,
                IsRead = false,
                CreatedDate = now,
                UpdatedDate = now,
                Sender = currentUser
            };

            await _unitOfWork.ChatMessages.AddAsync(message, cancellationToken);

            conversation.LastMessagePreview = preview;
            conversation.LastMessageAt = now;
            conversation.LastMessageSenderId = request.CurrentUserId;
            conversation.UnreadCountTarget += 1;
            conversation.UpdatedDate = now;

            _unitOfWork.ChatConversations.Update(conversation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var summaryForInitiator = ChatMappingHelper.MapConversationSummary(conversation, conversation.InitiatorUserId, request.CurrentUserRole);
            var summaryForStaffOrQueue = ChatMappingHelper.MapConversationSummary(
                conversation,
                conversation.AssignedStaffId ?? Guid.Empty,
                UserRole.CONSULTANT);
            var messageDto = ChatMappingHelper.MapMessage(message);

            bool isPendingUnclaimed = conversation.SupportStatus == SupportConversationStatus.PENDING && conversation.AssignedStaffId == null;
            if (isNewlyCreated && isPendingUnclaimed)
            {
                await _chatRealtimeService.NotifyNewPendingSupportConversationAsync(summaryForStaffOrQueue, cancellationToken);
            }

            await _chatRealtimeService.NotifyNewMessageAsync(
                summaryForInitiator,
                summaryForStaffOrQueue,
                messageDto,
                conversation.InitiatorUserId,
                conversation.AssignedStaffId,
                broadcastToSupportQueue: isPendingUnclaimed,
                cancellationToken);
        }
        else if (isNewlyCreated)
        {
            var summaryForQueue = ChatMappingHelper.MapConversationSummary(conversation, Guid.Empty, UserRole.CONSULTANT);
            await _chatRealtimeService.NotifyNewPendingSupportConversationAsync(summaryForQueue, cancellationToken);
        }

        return ChatMappingHelper.MapConversationSummary(conversation, request.CurrentUserId, request.CurrentUserRole);
    }
}
