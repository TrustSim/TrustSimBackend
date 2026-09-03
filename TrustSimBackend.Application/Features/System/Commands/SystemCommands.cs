using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId) : IRequest<ApiResponse<bool>>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, ApiResponse<bool>>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public MarkNotificationAsReadCommandHandler(INotificationRepository notificationRepository, IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await _notificationRepository.GetByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null)
        {
            throw new NotFoundException($"Không tìm thấy thông báo với ID '{request.NotificationId}'.");
        }

        if (notification.UserId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền thao tác trên thông báo này.");
        }

        notification.IsRead = true;
        _notificationRepository.Update(notification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Đánh dấu đã đọc thông báo thành công.");
    }
}

public record CreateDocumentCommand(
    string DocumentName,
    string LinkUrl
) : IRequest<ApiResponse<DocumentDto>>;

public class CreateDocumentCommandHandler : IRequestHandler<CreateDocumentCommand, ApiResponse<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDocumentCommandHandler(IDocumentRepository documentRepository, IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(CreateDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = new Document
        {
            Id = Guid.NewGuid(),
            DocumentName = request.DocumentName.Trim(),
            LinkUrl = request.LinkUrl.Trim()
        };

        await _documentRepository.AddAsync(doc, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new DocumentDto
        {
            Id = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            CreatedDate = doc.CreatedDate
        };

        return ApiResponse<DocumentDto>.SuccessResult(dto, "Tạo văn bản điều khoản thành công.");
    }
}

public record UpdateDocumentCommand(
    Guid Id,
    string DocumentName,
    string LinkUrl
) : IRequest<ApiResponse<DocumentDto>>;

public class UpdateDocumentCommandHandler : IRequestHandler<UpdateDocumentCommand, ApiResponse<DocumentDto>>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateDocumentCommandHandler(IDocumentRepository documentRepository, IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<DocumentDto>> Handle(UpdateDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await _documentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null)
        {
            throw new NotFoundException($"Không tìm thấy văn bản với ID '{request.Id}'.");
        }

        doc.DocumentName = request.DocumentName.Trim();
        doc.LinkUrl = request.LinkUrl.Trim();

        _documentRepository.Update(doc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new DocumentDto
        {
            Id = doc.Id,
            DocumentName = doc.DocumentName,
            LinkUrl = doc.LinkUrl,
            CreatedDate = doc.CreatedDate
        };

        return ApiResponse<DocumentDto>.SuccessResult(dto, "Cập nhật văn bản điều khoản thành công.");
    }
}
