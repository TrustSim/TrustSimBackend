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
/// Xử lý mở mới hoặc lấy lại cuộc hội thoại trực tiếp 1-1 (DIRECT) giữa hai người dùng đã xác thực danh tính (eKYC).
/// </summary>
public class GetOrCreateDirectConversationCommandHandler : IRequestHandler<GetOrCreateDirectConversationCommand, ChatConversationSummaryDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IChatRealtimeService _chatRealtimeService;

    public GetOrCreateDirectConversationCommandHandler(
        IUnitOfWork unitOfWork,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<ChatConversationSummaryDto> Handle(GetOrCreateDirectConversationCommand request, CancellationToken cancellationToken)
    {
        if (request.CurrentUserId == request.TargetUserId)
        {
            throw new BadRequestException("Không thể tự tạo cuộc hội thoại với chính mình.");
        }

        var currentUser = await _unitOfWork.Users.GetByIdAsync(request.CurrentUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.CurrentUserId}'.");

        if (!currentUser.IsActive || currentUser.IsDeleted)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động.");
        }

        if (currentUser.UserProfile == null || currentUser.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new UserProfileNotVerifiedException(
                "Chỉ những người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC) mới được phép nhắn tin.");
        }

        var targetUser = await _unitOfWork.Users.GetByIdAsync(request.TargetUserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy người dùng nhận tin nhắn với ID '{request.TargetUserId}'.");

        if (!targetUser.IsActive || targetUser.IsDeleted)
        {
            throw new BadRequestException("Tài khoản người nhận không còn hoạt động.");
        }

        if (targetUser.UserProfile == null || targetUser.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new BadRequestException("Chỉ có thể bắt đầu cuộc hội thoại với người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC).");
        }

        var conversation = await _unitOfWork.ChatConversations.GetDirectConversationBetweenUsersAsync(
            request.CurrentUserId,
            request.TargetUserId,
            cancellationToken);

        var now = DateTime.UtcNow;
        if (conversation == null)
        {
            conversation = new ChatConversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.DIRECT,
                InitiatorUserId = request.CurrentUserId,
                ParticipantUserId = request.TargetUserId,
                SupportStatus = SupportConversationStatus.NONE,
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
            conversation.UpdatedDate = now;

            if (conversation.InitiatorUserId == request.CurrentUserId)
            {
                conversation.UnreadCountTarget += 1;
            }
            else
            {
                conversation.UnreadCountInitiator += 1;
            }

            _unitOfWork.ChatConversations.Update(conversation);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var summaryForInitiator = ChatMappingHelper.MapConversationSummary(conversation, conversation.InitiatorUserId, UserRole.USER);
            var summaryForParticipant = conversation.ParticipantUserId.HasValue
                ? ChatMappingHelper.MapConversationSummary(conversation, conversation.ParticipantUserId.Value, UserRole.USER)
                : null;
            var messageDto = ChatMappingHelper.MapMessage(message);

            await _chatRealtimeService.NotifyNewMessageAsync(
                summaryForInitiator,
                summaryForParticipant,
                messageDto,
                conversation.InitiatorUserId,
                conversation.ParticipantUserId,
                broadcastToSupportQueue: false,
                cancellationToken);
        }

        return ChatMappingHelper.MapConversationSummary(conversation, request.CurrentUserId, request.CurrentUserRole);
    }
}
