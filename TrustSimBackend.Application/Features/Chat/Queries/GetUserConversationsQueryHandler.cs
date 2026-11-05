using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Application.Features.Chat.Helpers;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Chat.Queries;

/// <summary>
/// Xử lý lấy danh sách các cuộc hội thoại của người dùng hiện tại.
/// Tuân thủ tuyệt đối quy tắc bảo mật: Hội thoại SUPPORT đã được một Nhân viên/Admin duyệt nhận sẽ hoàn toàn ẩn khỏi mọi Nhân viên/Admin khác.
/// </summary>
public class GetUserConversationsQueryHandler : IRequestHandler<GetUserConversationsQuery, PagedResult<ChatConversationSummaryDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUserConversationsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ChatConversationSummaryDto>> Handle(GetUserConversationsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

        var (conversations, totalCount) = await _unitOfWork.ChatConversations.GetUserConversationsPagedAsync(
            request.CurrentUserId,
            request.CurrentUserRole,
            request.Type,
            request.SupportStatus,
            pageIndex,
            pageSize,
            cancellationToken);

        var dtos = conversations
            .Select(c => ChatMappingHelper.MapConversationSummary(c, request.CurrentUserId, request.CurrentUserRole))
            .ToList();

        return PagedResult<ChatConversationSummaryDto>.Create(dtos, totalCount, pageIndex, pageSize);
    }
}
