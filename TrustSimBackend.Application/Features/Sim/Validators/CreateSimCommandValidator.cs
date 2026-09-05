using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class CreateSimCommandValidator : AbstractValidator<CreateSimCommand>
{
    public CreateSimCommandValidator()
    {
        RuleFor(v => v.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0|\+84)(3|5|7|8|9)[0-9]{8}$").WithMessage("Số điện thoại không đúng định dạng di động Việt Nam (VD: 0981234567).");

        RuleFor(v => v.PhonePrefixId)
            .NotEmpty().WithMessage("PhonePrefixId không được để trống.");

        RuleFor(v => v.CategoryIds)
            .Must(ids => ids == null || ids.Count > 0)
            .WithMessage("Danh sách CategoryIds nếu có phải chứa ít nhất 1 mục.");
    }
}
