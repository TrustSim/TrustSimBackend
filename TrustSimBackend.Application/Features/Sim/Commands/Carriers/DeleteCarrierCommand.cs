using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Carriers;

public record DeleteCarrierCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeleteCarrierCommandHandler : IRequestHandler<DeleteCarrierCommand, ApiResponse<bool>>
{
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCarrierCommandHandler(ISimCarrierRepository carrierRepository, IUnitOfWork unitOfWork)
    {
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteCarrierCommand request, CancellationToken cancellationToken)
    {
        var carrier = await _carrierRepository.GetByIdAsync(request.Id, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.Id}'.");
        }

        // Xóa mềm (Soft Delete)
        carrier.IsDeleted = true;
        carrier.DeletedDate = DateTime.UtcNow;

        _carrierRepository.Update(carrier);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Xóa nhà mạng thành công.");
    }
}
