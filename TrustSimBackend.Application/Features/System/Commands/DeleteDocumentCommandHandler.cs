using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDocumentCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await _unitOfWork.Documents.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null) throw new NotFoundException($"Không tìm thấy tài liệu với ID '{request.Id}'.");

        doc.IsDeleted = true;
        doc.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.Documents.Update(doc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
