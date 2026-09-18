using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeleteSimCommandHandler : IRequestHandler<DeleteSimCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSimCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteSimCommand request, CancellationToken cancellationToken)
    {
        var sim = await _unitOfWork.SimNumbers.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        sim.IsDeleted = true;
        sim.DeletedDate = DateTime.UtcNow;

        _unitOfWork.SimNumbers.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
