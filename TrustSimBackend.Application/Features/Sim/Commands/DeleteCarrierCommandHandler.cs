using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class DeleteCarrierCommandHandler : IRequestHandler<DeleteCarrierCommand, bool>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCarrierCommandHandler(ISimCarrierRepository carrierRepository, IUnitOfWork unitOfWork)
    {
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteCarrierCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _carrierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        carrier.IsDeleted = true;
        carrier.DeletedDate = DateTime.UtcNow;

        _carrierRepository.Update(carrier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
