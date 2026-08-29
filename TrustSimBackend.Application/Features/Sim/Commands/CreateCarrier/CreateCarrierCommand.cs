using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.CreateCarrier;

public record CreateCarrierCommand(
    string CarrierName,
    string CarrierCode,
    string LogoImg,
    List<string>? Prefixes = null
) : IRequest<ApiResponse<SimCarrierDto>>;

public class CreateCarrierCommandValidator : AbstractValidator<CreateCarrierCommand>
{
    public CreateCarrierCommandValidator()
    {
        RuleFor(x => x.CarrierName)
            .NotEmpty().WithMessage("Tên nhà mạng không được để trống.")
            .MaximumLength(100).WithMessage("Tên nhà mạng không được vượt quá 100 ký tự.");

        RuleFor(x => x.CarrierCode)
            .NotEmpty().WithMessage("Mã nhà mạng không được để trống.")
            .MaximumLength(20).WithMessage("Mã nhà mạng không được vượt quá 20 ký tự.");
    }
}

public class CreateCarrierCommandHandler : IRequestHandler<CreateCarrierCommand, ApiResponse<SimCarrierDto>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCarrierCommandHandler(
        ISimMasterDataRepository masterDataRepository,
        IUnitOfWork unitOfWork)
    {
        _masterDataRepository = masterDataRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCarrierDto>> Handle(CreateCarrierCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CarrierCode.Trim().ToUpper();

        if (await _masterDataRepository.ExistsCarrierCodeAsync(normalizedCode, cancellationToken))
        {
            throw new BadRequestException($"Mã nhà mạng '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var carrierId = Guid.NewGuid();
        var carrier = new SimCarrier
        {
            Id = carrierId,
            CarrierName = request.CarrierName.Trim(),
            CarrierCode = normalizedCode,
            LogoImg = request.LogoImg?.Trim() ?? string.Empty,
            IsActive = true
        };

        if (request.Prefixes != null && request.Prefixes.Any())
        {
            foreach (var prefix in request.Prefixes.Distinct())
            {
                var cleanPrefix = prefix.Trim();
                if (!string.IsNullOrWhiteSpace(cleanPrefix))
                {
                    carrier.PhonePrefixes.Add(new PhonePrefix
                    {
                        Id = Guid.NewGuid(),
                        PrefixValue = cleanPrefix,
                        SimCarrierId = carrierId,
                        IsActive = true
                    });
                }
            }
        }

        await _masterDataRepository.AddCarrierAsync(carrier, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimCarrierDto
        {
            Id = carrier.Id,
            CarrierName = carrier.CarrierName,
            CarrierCode = carrier.CarrierCode,
            LogoImg = carrier.LogoImg,
            IsActive = carrier.IsActive,
            Prefixes = carrier.PhonePrefixes.Select(p => new PhonePrefixDto
            {
                Id = p.Id,
                PrefixValue = p.PrefixValue,
                SimCarrierId = carrier.Id,
                CarrierName = carrier.CarrierName,
                IsActive = p.IsActive
            }).ToList()
        };

        return ApiResponse<SimCarrierDto>.SuccessResult(dto, "Tạo nhà mạng mới thành công.");
    }
}
