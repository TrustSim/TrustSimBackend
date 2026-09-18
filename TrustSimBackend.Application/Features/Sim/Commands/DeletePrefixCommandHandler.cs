using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeletePrefixCommandHandler : IRequestHandler<DeletePrefixCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeletePrefixCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeletePrefixCommand request, CancellationToken cancellationToken)
    {
        var prefix = await _unitOfWork.PhonePrefixes.GetByIdAsync(request.Id, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.Id}'.");
        }

        prefix.IsDeleted = true;
        prefix.DeletedDate = DateTime.UtcNow;

        _unitOfWork.PhonePrefixes.Update(prefix);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
