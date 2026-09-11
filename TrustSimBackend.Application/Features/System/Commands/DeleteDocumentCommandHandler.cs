using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.System.Commands;

public class DeleteDocumentCommandHandler : IRequestHandler<DeleteDocumentCommand, bool>
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteDocumentCommandHandler(IDocumentRepository documentRepository, IUnitOfWork unitOfWork)
    {
        _documentRepository = documentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteDocumentCommand request, CancellationToken cancellationToken)
    {
        var doc = await _documentRepository.GetByIdAsync(request.Id, cancellationToken);
        if (doc == null) throw new NotFoundException($"Không tìm thấy tài liệu với ID '{request.Id}'.");

        doc.IsDeleted = true;
        doc.UpdatedDate = DateTime.UtcNow;

        _documentRepository.Update(doc);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
