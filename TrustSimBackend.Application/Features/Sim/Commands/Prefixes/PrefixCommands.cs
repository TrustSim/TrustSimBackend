using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Prefixes;

public record CreatePrefixCommand(
    string PrefixValue,
    Guid SimCarrierId
) : IRequest<ApiResponse<PhonePrefixDto>>;

public class CreatePrefixCommandHandler : IRequestHandler<CreatePrefixCommand, ApiResponse<PhonePrefixDto>>
{
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly ISimCarrierRepository _carrierRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePrefixCommandHandler(
        IPhonePrefixRepository prefixRepository,
        ISimCarrierRepository carrierRepository,
        IUnitOfWork unitOfWork)
    {
        _prefixRepository = prefixRepository;
        _carrierRepository = carrierRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<PhonePrefixDto>> Handle(CreatePrefixCommand request, CancellationToken cancellationToken)
    {
        var cleanValue = request.PrefixValue.Trim();
        if (await _prefixRepository.ExistsByValueAsync(cleanValue, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Đầu số '{cleanValue}' đã tồn tại trong hệ thống.");
        }

        var carrier = await _carrierRepository.GetByIdAsync(request.SimCarrierId, cancellationToken);
        if (carrier == null)
        {
            throw new NotFoundException($"Không tìm thấy nhà mạng với ID '{request.SimCarrierId}'.");
        }

        var prefix = new PhonePrefix
        {
            Id = Guid.NewGuid(),
            PrefixValue = cleanValue,
            SimCarrierId = request.SimCarrierId
        };

        await _prefixRepository.AddAsync(prefix, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new PhonePrefixDto
        {
            Id = prefix.Id,
            PrefixValue = prefix.PrefixValue,
            SimCarrierId = prefix.SimCarrierId,
            CarrierName = carrier.CarrierName
        };

        return ApiResponse<PhonePrefixDto>.SuccessResult(dto, "Thêm mới đầu số thành công.");
    }
}

public record DeletePrefixCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeletePrefixCommandHandler : IRequestHandler<DeletePrefixCommand, ApiResponse<bool>>
{
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeletePrefixCommandHandler(IPhonePrefixRepository prefixRepository, IUnitOfWork unitOfWork)
    {
        _prefixRepository = prefixRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeletePrefixCommand request, CancellationToken cancellationToken)
    {
        var prefix = await _prefixRepository.GetByIdAsync(request.Id, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.Id}'.");
        }

        prefix.IsDeleted = true;
        prefix.DeletedDate = DateTime.UtcNow;

        _prefixRepository.Update(prefix);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Xóa đầu số thành công.");
    }
}
