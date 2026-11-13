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
/// - Nếu đang có cuộc hội thoại PENDING (chưa ai duyệt): giữ nguyên cuộc hội thoại PENDING đó.
/// - Nếu cuộc hội thoại trước đó đã được Sàn duyệt (CLAIMED): tự động chuyển phiên cũ sang RESOLVED và tạo một đoạn chat mới hoàn toàn (PENDING, AssignedStaffId = null) để đưa vào hàng đợi duyệt lại (có thể được duyệt bởi nhân viên/admin khác).
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

        if (currentUser.Role == UserRole.ADMIN || currentUser.Role == UserRole.CONSULTANT || request.CurrentUserRole != UserRole.USER)
        {
            throw new ForbiddenAccessException(
                "Nhân viên Sàn và Quản trị viên không được phép tạo cuộc hội thoại yêu cầu hỗ trợ với Sàn.");
        }

        if (currentUser.UserProfile == null || currentUser.UserProfile.VerificationStatus != VerificationStatus.VERIFIED)
        {
            throw new UserProfileNotVerifiedException(
                "Chỉ những người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC) mới được phép nhắn tin hỗ trợ.");
        }

        var now = DateTime.UtcNow;

        // 1. Tìm tất cả các cuộc hội thoại SUPPORT của người dùng đã được duyệt trước đó (CLAIMED)
        var claimedList = new List<ChatConversation>();
        var repoClaimed = await _unitOfWork.ChatConversations.GetClaimedSupportConversationsByUserIdAsync(
            request.CurrentUserId,
            cancellationToken);

        if (repoClaimed != null && repoClaimed.Count > 0)
        {
            claimedList.AddRange(repoClaimed);
        }

        var activeConv = await _unitOfWork.ChatConversations.GetActiveSupportConversationByUserIdAsync(
            request.CurrentUserId,
            cancellationToken);

        if (activeConv != null
            && activeConv.SupportStatus == SupportConversationStatus.CLAIMED
            && claimedList.All(c => c.Id != activeConv.Id))
        {
            claimedList.Add(activeConv);
        }

        // 2. Nếu có phiên hỗ trợ trước đó đã được Sàn duyệt (CLAIMED), tự động đóng (RESOLVED) phiên cũ
        // để khi người dùng bấm nút "Chat với sàn" sẽ tạo đoạn chat mới hoàn toàn và được duyệt lại bởi bất kỳ nhân viên/admin nào.
        if (claimedList.Count > 0)
        {
            foreach (var oldConv in claimedList)
            {
                oldConv.SupportStatus = SupportConversationStatus.RESOLVED;
                oldConv.UpdatedDate = now;

                var closeMsgContent = "Phiên hỗ trợ đã kết thúc do khách hàng bắt đầu cuộc hội thoại hỗ trợ mới với Sàn.";
                var closeSystemMessage = new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    ConversationId = oldConv.Id,
                    SenderId = request.CurrentUserId,
                    MessageType = ChatMessageType.SYSTEM,
                    Content = closeMsgContent,
                    IsRead = false,
                    CreatedDate = now,
                    UpdatedDate = now,
                    Sender = currentUser
                };

                await _unitOfWork.ChatMessages.AddAsync(closeSystemMessage, cancellationToken);

                oldConv.LastMessagePreview = closeMsgContent;
                oldConv.LastMessageAt = now;
                oldConv.LastMessageSenderId = request.CurrentUserId;

                _unitOfWork.ChatConversations.Update(oldConv);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // 3. Kiểm tra xem có cuộc hội thoại nào đang chờ duyệt (PENDING & chưa ai nhận) hay không
        var conversation = await _unitOfWork.ChatConversations.GetPendingSupportConversationByUserIdAsync(
            request.CurrentUserId,
            cancellationToken);

        if (conversation == null
            && activeConv != null
            && activeConv.SupportStatus == SupportConversationStatus.PENDING
            && activeConv.AssignedStaffId == null)
        {
            conversation = activeConv;
        }

        bool isNewlyCreated = false;

        if (conversation == null)
        {
            isNewlyCreated = true;
            conversation = new ChatConversation
            {
                Id = Guid.NewGuid(),
                Type = ConversationType.SUPPORT,
                InitiatorUserId = request.CurrentUserId,
                InitiatorUser = currentUser,
                ParticipantUserId = null,
                AssignedStaffId = null,
                ClaimedAt = null,
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
