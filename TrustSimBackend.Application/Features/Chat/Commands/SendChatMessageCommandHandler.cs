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
/// Xử lý gửi tin nhắn văn bản hoặc đính kèm tệp (Ảnh / PDF đã kiểm định Font chữ đa thiết bị) trong cuộc hội thoại.
/// Kiểm tra nghiêm ngặt quyền riêng tư cho cả Direct Chat và Support Chat.
/// </summary>
public class SendChatMessageCommandHandler : IRequestHandler<SendChatMessageCommand, ChatMessageDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly IPdfFontValidatorService _pdfFontValidatorService;
    private readonly IChatRealtimeService _chatRealtimeService;

    private static readonly string[] AllowedAttachmentExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    public SendChatMessageCommandHandler(
        IUnitOfWork unitOfWork,
        ICloudStorageService cloudStorageService,
        IPdfFontValidatorService pdfFontValidatorService,
        IChatRealtimeService chatRealtimeService)
    {
        _unitOfWork = unitOfWork;
        _cloudStorageService = cloudStorageService;
        _pdfFontValidatorService = pdfFontValidatorService;
        _chatRealtimeService = chatRealtimeService;
    }

    public async Task<ChatMessageDto> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
    {
        var sender = await _unitOfWork.Users.GetByIdAsync(request.SenderId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người gửi với ID '{request.SenderId}'.");

        if (!sender.IsActive || sender.IsDeleted)
        {
            throw new ForbiddenAccessException("Tài khoản của bạn đã bị khóa hoặc ngừng hoạt động.");
        }

        if (request.SenderRole == UserRole.USER &&
            (sender.UserProfile == null || sender.UserProfile.VerificationStatus != VerificationStatus.VERIFIED))
        {
            throw new UserProfileNotVerifiedException(
                "Chỉ những người dùng đã tạo tài khoản và hoàn tất xác thực danh tính (eKYC) mới được phép gửi tin nhắn.");
        }

        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại với ID '{request.ConversationId}'.");

        // Kiểm tra quyền truy cập nghiêm ngặt (chặn tuyệt đối Nhân viên/Admin khác nếu cuộc hội thoại đã được người khác duyệt)
        if (!conversation.CanUserAccess(request.SenderId, request.SenderRole))
        {
            throw new ForbiddenAccessException(
                "Cuộc hội thoại này là riêng tư hoặc đã được một nhân viên/quản trị viên khác tiếp nhận xử lý. Bạn không có quyền gửi tin nhắn hoặc can thiệp.");
        }

        if (conversation.Type == ConversationType.DIRECT)
        {
            var initiatorVerified = conversation.InitiatorUser?.UserProfile?.VerificationStatus == VerificationStatus.VERIFIED;
            var participantVerified = conversation.ParticipantUser?.UserProfile?.VerificationStatus == VerificationStatus.VERIFIED;
            if (!initiatorVerified || !participantVerified)
            {
                throw new BadRequestException("Cuộc hội thoại giữa người dùng yêu cầu cả hai bên đều đã xác thực danh tính (eKYC).");
            }
        }
        else if (conversation.Type == ConversationType.SUPPORT)
        {
            // Nếu là nhân viên/admin muốn trả lời nhưng chưa bấm Duyệt/Tiếp nhận (AssignedStaffId == null)
            if (request.SenderId != conversation.InitiatorUserId && conversation.AssignedStaffId == null)
            {
                throw new BadRequestException(
                    "Vui lòng bấm Chấp nhận / Duyệt tiếp nhận cuộc hội thoại hỗ trợ trước khi gửi tin nhắn trả lời khách hàng.");
            }
        }

        string? attachmentUrl = null;
        string? attachmentFileName = null;
        var messageType = ChatMessageType.TEXT;

        if (request.AttachmentFile != null && request.AttachmentFile.Length > 0)
        {
            if (request.AttachmentFile.Length > 25 * 1024 * 1024)
            {
                throw new BadRequestException("Kích thước tệp đính kèm không được vượt quá 25MB.");
            }

            attachmentFileName = request.AttachmentFile.FileName;
            var ext = Path.GetExtension(attachmentFileName).ToLowerInvariant();
            if (!AllowedAttachmentExtensions.Contains(ext))
            {
                throw new BadRequestException($"Định dạng tệp '{ext}' không được hỗ trợ trong khung chat. Chỉ chấp nhận: {string.Join(", ", AllowedAttachmentExtensions)}");
            }

            if (ext == ".pdf")
            {
                messageType = ChatMessageType.PDF_DOCUMENT;
                using var ms = new MemoryStream();
                await request.AttachmentFile.CopyToAsync(ms, cancellationToken);
                ms.Position = 0;

                // Kiểm định tính tương thích Font chữ đa thiết bị trước khi cho phép gửi PDF trong Chat
                await _pdfFontValidatorService.EnsureCrossDeviceCompatibleAsync(ms, attachmentFileName, cancellationToken);

                var pdfFileName = $"{Guid.NewGuid():N}_{attachmentFileName}";
                attachmentUrl = await _cloudStorageService.UploadImageBytesAsync(
                    ms.ToArray(),
                    pdfFileName,
                    "chat-attachments",
                    "application/pdf",
                    cancellationToken);
            }
            else
            {
                messageType = ChatMessageType.IMAGE;
                attachmentUrl = await _cloudStorageService.UploadImageAsync(
                    request.AttachmentFile,
                    "chat-attachments",
                    cancellationToken);
            }
        }

        var trimmedContent = request.Content?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedContent) && !string.IsNullOrEmpty(attachmentFileName))
        {
            trimmedContent = messageType == ChatMessageType.PDF_DOCUMENT
                ? $"[Tài liệu PDF] {attachmentFileName}"
                : $"[Hình ảnh] {attachmentFileName}";
        }

        var now = DateTime.UtcNow;
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            ConversationId = conversation.Id,
            SenderId = request.SenderId,
            MessageType = messageType,
            Content = trimmedContent,
            AttachmentUrl = attachmentUrl,
            AttachmentFileName = attachmentFileName,
            IsRead = false,
            CreatedDate = now,
            UpdatedDate = now,
            Sender = sender
        };

        await _unitOfWork.ChatMessages.AddAsync(message, cancellationToken);

        var preview = trimmedContent.Length > 450 ? trimmedContent[..450] + "..." : trimmedContent;
        conversation.LastMessagePreview = preview;
        conversation.LastMessageAt = now;
        conversation.LastMessageSenderId = request.SenderId;
        conversation.UpdatedDate = now;

        if (request.SenderId == conversation.InitiatorUserId)
        {
            conversation.UnreadCountTarget += 1;
        }
        else
        {
            conversation.UnreadCountInitiator += 1;
        }

        _unitOfWork.ChatConversations.Update(conversation);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var messageDto = ChatMappingHelper.MapMessage(message);
        var summaryForInitiator = ChatMappingHelper.MapConversationSummary(
            conversation,
            conversation.InitiatorUserId,
            UserRole.USER);

        Guid? targetUserId = conversation.Type == ConversationType.DIRECT
            ? conversation.ParticipantUserId
            : conversation.AssignedStaffId;

        var targetViewerRole = conversation.Type == ConversationType.DIRECT
            ? UserRole.USER
            : (conversation.AssignedStaff?.Role ?? UserRole.CONSULTANT);

        var summaryForTargetOrQueue = ChatMappingHelper.MapConversationSummary(
            conversation,
            targetUserId ?? Guid.Empty,
            targetViewerRole);

        bool broadcastToSupportQueue = conversation.Type == ConversationType.SUPPORT
                                    && conversation.SupportStatus == SupportConversationStatus.PENDING
                                    && conversation.AssignedStaffId == null;

        await _chatRealtimeService.NotifyNewMessageAsync(
            summaryForInitiator,
            summaryForTargetOrQueue,
            messageDto,
            conversation.InitiatorUserId,
            targetUserId,
            broadcastToSupportQueue,
            cancellationToken);

        return messageDto;
    }
}
