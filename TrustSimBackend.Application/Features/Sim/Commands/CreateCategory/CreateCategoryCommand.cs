using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.CreateCategory;

public record CreateCategoryCommand(
    string CategoryCode,
    string CategoryName,
    string Description,
    float ValuationMultiplier = 1.0f
) : IRequest<ApiResponse<SimCategoryDto>>;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryCode)
            .NotEmpty().WithMessage("Mã danh mục không được để trống.")
            .MaximumLength(50).WithMessage("Mã danh mục không được vượt quá 50 ký tự.");

        RuleFor(x => x.CategoryName)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục không được vượt quá 100 ký tự.");

        RuleFor(x => x.ValuationMultiplier)
            .GreaterThan(0).WithMessage("Hệ số định giá phải lớn hơn 0.");
    }
}

public class CreateCategoryCommandHandler : IRequestHandler<CreateCategoryCommand, ApiResponse<SimCategoryDto>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateCategoryCommandHandler(
        ISimMasterDataRepository masterDataRepository,
        IUnitOfWork unitOfWork)
    {
        _masterDataRepository = masterDataRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<SimCategoryDto>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.CategoryCode.Trim().ToUpper();

        if (await _masterDataRepository.ExistsCategoryCodeAsync(normalizedCode, cancellationToken))
        {
            throw new BadRequestException($"Mã danh mục '{normalizedCode}' đã tồn tại trong hệ thống.");
        }

        var category = new SimCategory
        {
            Id = Guid.NewGuid(),
            CategoryCode = normalizedCode,
            CategoryName = request.CategoryName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ValuationMultiplier = request.ValuationMultiplier,
            IsActive = true
        };

        await _masterDataRepository.AddCategoryAsync(category, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = new SimCategoryDto
        {
            Id = category.Id,
            CategoryCode = category.CategoryCode,
            CategoryName = category.CategoryName,
            Description = category.Description,
            ValuationMultiplier = category.ValuationMultiplier,
            IsActive = category.IsActive
        };

        return ApiResponse<SimCategoryDto>.SuccessResult(dto, "Tạo danh mục SIM mới thành công.");
    }
}
