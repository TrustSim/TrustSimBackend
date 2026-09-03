using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Queries;

public record GetMyNotificationsQuery(
    Guid UserId,
    bool? IsRead = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<NotificationDto>>>;

public class GetMyNotificationsQueryHandler : IRequestHandler<GetMyNotificationsQuery, ApiResponse<PagedResult<NotificationDto>>>
{
    private readonly INotificationRepository _notificationRepository;

    public GetMyNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    public async Task<ApiResponse<PagedResult<NotificationDto>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _notificationRepository.GetPagedByUserIdAsync(
            request.UserId,
            request.IsRead,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(n => new NotificationDto
        {
            Id = n.Id,
            UserId = n.UserId,
            Title = n.Title,
            Content = n.Content,
            ActionUrl = n.ActionUrl,
            IsRead = n.IsRead,
            CreatedDate = n.CreatedDate
        }).ToList();

        var pagedResult = PagedResult<NotificationDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<NotificationDto>>.SuccessResult(pagedResult, "Lấy danh sách thông báo thành công.");
    }
}

public record GetDocumentsQuery(bool ActiveOnly = true) : IRequest<ApiResponse<IReadOnlyList<DocumentDto>>>;

public class GetDocumentsQueryHandler : IRequestHandler<GetDocumentsQuery, ApiResponse<IReadOnlyList<DocumentDto>>>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentsQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<DocumentDto>>> Handle(GetDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _documentRepository.GetAllAsync(request.ActiveOnly, cancellationToken);
        var dtos = docs.Select(d => new DocumentDto
        {
            Id = d.Id,
            DocumentName = d.DocumentName,
            LinkUrl = d.LinkUrl,
            CreatedDate = d.CreatedDate
        }).ToList();

        return ApiResponse<IReadOnlyList<DocumentDto>>.SuccessResult(dtos, "Lấy danh sách văn bản thành công.");
    }
}

public record GetDocumentByIdQuery(Guid Id) : IRequest<ApiResponse<DocumentDto>>;

public class GetDocumentByIdQueryHandler : IRequestHandler<GetDocumentByIdQuery, ApiResponse<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;

    public GetDocumentByIdQueryHandler(IDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(GetDocumentByIdQuery request, CancellationToken cancellationToken)
    {
        var doc = await _documentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null)
        {
            throw new NotFoundException($"Không tìm thấy văn bản với ID '{request.Id}'.");
        }

        var dto = new DocumentDto
        {
            Id = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            CreatedDate = doc.CreatedDate
        };

        return ApiResponse<DocumentDto>.SuccessResult(dto, "Lấy chi tiết văn bản thành công.");
    }
}
