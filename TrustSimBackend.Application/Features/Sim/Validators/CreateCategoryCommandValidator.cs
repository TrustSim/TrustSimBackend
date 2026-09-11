using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
{
    public CreateCategoryCommandValidator()
    {
        RuleFor(v => v.CategoryCode)
            .NotEmpty().WithMessage("Mã danh mục không được để trống.")
            .MaximumLength(20).WithMessage("Mã danh mục không vượt quá 20 ký tự.")
            .Matches(@"^[A-Z0-9_]+$").WithMessage("Mã danh mục chỉ chứa chữ hoa, chữ số và dấu gạch dưới.");

        RuleFor(v => v.CategoryName)
            .NotEmpty().WithMessage("Tên danh mục không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục không vượt quá 100 ký tự.");

        RuleFor(v => v.Description)
            .MaximumLength(500).WithMessage("Mô tả không vượt quá 500 ký tự.")
            .When(v => !string.IsNullOrWhiteSpace(v.Description));

        RuleFor(v => v.ValuationMultiplier)
            .GreaterThan(0).WithMessage("Hệ số định giá phải lớn hơn 0.")
            .LessThanOrEqualTo(100).WithMessage("Hệ số định giá không vượt quá 100.");
    }
}
