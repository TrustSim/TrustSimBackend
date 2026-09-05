using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeleteSimCommandHandler : IRequestHandler<DeleteSimCommand, bool>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSimCommandHandler(ISimNumberRepository simRepository, IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteSimCommand request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        sim.IsDeleted = true;
        sim.DeletedDate = DateTime.UtcNow;

        _simRepository.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
