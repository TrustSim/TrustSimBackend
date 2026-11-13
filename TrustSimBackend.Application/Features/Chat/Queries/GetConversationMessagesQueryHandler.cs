using MediatR;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Application.Features.Chat.Helpers;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Chat.Queries;

/// <summary>
/// Xử lý lấy danh sách lịch sử tin nhắn trong một cuộc hội thoại.
/// Chặn 403 Forbidden tuyệt đối nếu người dùng không thuộc cuộc hội thoại hoặc khi cuộc hội thoại Support đã được Nhân viên/Admin khác duyệt nhận.
/// </summary>
public class GetConversationMessagesQueryHandler : IRequestHandler<GetConversationMessagesQuery, PagedResult<ChatMessageDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly MinioSettings _minioSettings;

    public GetConversationMessagesQueryHandler(
        IUnitOfWork unitOfWork,
        IOptions<MinioSettings>? minioOptions = null)
    {
        _unitOfWork = unitOfWork;
        _minioSettings = minioOptions?.Value ?? new MinioSettings();
    }

    public async Task<PagedResult<ChatMessageDto>> Handle(GetConversationMessagesQuery request, CancellationToken cancellationToken)
    {
        var conversation = await _unitOfWork.ChatConversations.GetByIdWithDetailsAsync(request.ConversationId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy cuộc hội thoại với ID '{request.ConversationId}'.");

        if (!conversation.CanUserAccess(request.CurrentUserId, request.CurrentUserRole))
        {
            throw new ForbiddenAccessException(
                "Cuộc hội thoại này là riêng tư hoặc đã được một nhân viên/quản trị viên khác tiếp nhận xử lý. Bạn không có quyền xem hoặc can thiệp.");
        }

        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 50 : Math.Min(request.PageSize, 100);

        var (messages, totalCount) = await _unitOfWork.ChatMessages.GetMessagesByConversationPagedAsync(
            request.ConversationId,
            pageIndex,
            pageSize,
            cancellationToken);

        var retentionDays = _minioSettings.ChatImageExpirationDays > 0
            ? _minioSettings.ChatImageExpirationDays
            : ChatMappingHelper.DefaultChatImageRetentionDays;

        var dtos = messages
            .Select(m => ChatMappingHelper.MapMessageWithRetention(m, retentionDays))
            .ToList();

        return PagedResult<ChatMessageDto>.Create(dtos, totalCount, pageIndex, pageSize);
    }
}
