using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.Sims;

public record CreateSimCommand(
    string PhoneNumber,
    Guid PhonePrefixId,
    Guid? FormatRhythmId,
    List<Guid>? CategoryIds
) : IRequest<ApiResponse<SimNumberDto>>;

public class CreateSimCommandHandler : IRequestHandler<CreateSimCommand, ApiResponse<SimNumberDto>>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSimCommandHandler(
        ISimNumberRepository simRepository,
        IPhonePrefixRepository prefixRepository,
        IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _prefixRepository = prefixRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimNumberDto>> Handle(CreateSimCommand request, CancellationToken cancellationToken)
    {
        var cleanNumber = request.PhoneNumber.Trim();
        if (await _simRepository.ExistsByPhoneNumberAsync(cleanNumber, cancellationToken: cancellationToken))
        {
            throw new BadRequestException($"Số SIM '{cleanNumber}' đã tồn tại trong hệ thống.");
        }

        var prefix = await _prefixRepository.GetByIdAsync(request.PhonePrefixId, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.PhonePrefixId}'.");
        }

        var rhythmId = request.FormatRhythmId.HasValue && request.FormatRhythmId.Value != Guid.Empty
            ? request.FormatRhythmId.Value
            : await _simRepository.GetOrCreateDefaultFormatRhythmIdAsync(cancellationToken);

        var sim = new SimNumber
        {
            Id = Guid.NewGuid(),
            PhoneNumber = cleanNumber,
            SimStatus = SimStatus.AVAILABLE,
            PhonePrefixId = request.PhonePrefixId,
            FormatRhythmId = rhythmId
        };

        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            foreach (var catId in request.CategoryIds.Distinct())
            {
                sim.SimCategoryMappings.Add(new SimCategoryMapping
                {
                    Id = Guid.NewGuid(),
                    SimNumberId = sim.Id,
                    SimCategoryId = catId
                });
            }
        }

        await _simRepository.AddAsync(sim, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimNumberDto
        {
            Id = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            SimStatus = sim.SimStatus,
            PhonePrefixId = sim.PhonePrefixId,
            PrefixValue = prefix.PrefixValue,
            CarrierName = prefix.SimCarrier?.CarrierName ?? string.Empty,
            FormatRhythmId = sim.FormatRhythmId,
            CreatedDate = sim.CreatedDate
        };

        return ApiResponse<SimNumberDto>.SuccessResult(dto, "Thêm mới số SIM thành công.");
    }
}

public record UpdateSimCommand(
    Guid Id,
    SimStatus SimStatus,
    Guid PhonePrefixId,
    Guid? FormatRhythmId,
    List<Guid>? CategoryIds
) : IRequest<ApiResponse<SimNumberDto>>;

public class UpdateSimCommandHandler : IRequestHandler<UpdateSimCommand, ApiResponse<SimNumberDto>>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IPhonePrefixRepository _prefixRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateSimCommandHandler(
        ISimNumberRepository simRepository,
        IPhonePrefixRepository prefixRepository,
        IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _prefixRepository = prefixRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimNumberDto>> Handle(UpdateSimCommand request, CancellationToken cancellationToken)
    {
        var sim = await _simRepository.GetByIdAsync(request.Id, cancellationToken);
        if (sim == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM với ID '{request.Id}'.");
        }

        var prefix = await _prefixRepository.GetByIdAsync(request.PhonePrefixId, cancellationToken);
        if (prefix == null)
        {
            throw new NotFoundException($"Không tìm thấy đầu số với ID '{request.PhonePrefixId}'.");
        }

        var rhythmId = request.FormatRhythmId.HasValue && request.FormatRhythmId.Value != Guid.Empty
            ? request.FormatRhythmId.Value
            : sim.FormatRhythmId;

        sim.SimStatus = request.SimStatus;
        sim.PhonePrefixId = request.PhonePrefixId;
        sim.FormatRhythmId = rhythmId;

        // Cập nhật mapping category
        sim.SimCategoryMappings.Clear();
        if (request.CategoryIds != null && request.CategoryIds.Any())
        {
            foreach (var catId in request.CategoryIds.Distinct())
            {
                sim.SimCategoryMappings.Add(new SimCategoryMapping
                {
                    Id = Guid.NewGuid(),
                    SimNumberId = sim.Id,
                    SimCategoryId = catId
                });
            }
        }

        _simRepository.Update(sim);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimNumberDto
        {
            Id = sim.Id,
            PhoneNumber = sim.PhoneNumber,
            SimStatus = sim.SimStatus,
            PhonePrefixId = sim.PhonePrefixId,
            PrefixValue = prefix.PrefixValue,
            CarrierName = prefix.SimCarrier?.CarrierName ?? string.Empty,
            FormatRhythmId = sim.FormatRhythmId,
            CreatedDate = sim.CreatedDate
        };

        return ApiResponse<SimNumberDto>.SuccessResult(dto, "Cập nhật số SIM thành công.");
    }
}

public record DeleteSimCommand(Guid Id) : IRequest<ApiResponse<bool>>;

public class DeleteSimCommandHandler : IRequestHandler<DeleteSimCommand, ApiResponse<bool>>
{
    private readonly ISimNumberRepository _simRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteSimCommandHandler(ISimNumberRepository simRepository, IUnitOfWork unitOfWork)
    {
        _simRepository = simRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(DeleteSimCommand request, CancellationToken cancellationToken)
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

        return ApiResponse<bool>.SuccessResult(true, "Xóa số SIM thành công.");
    }
}
