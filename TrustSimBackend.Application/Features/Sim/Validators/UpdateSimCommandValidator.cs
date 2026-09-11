using FluentValidation;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Sim.Validators;

public class UpdateSimCommandValidator : AbstractValidator<UpdateSimCommand>
{
    public UpdateSimCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty().WithMessage("Id SIM không được để trống.");

        RuleFor(v => v.PhonePrefixId)
            .NotEmpty().WithMessage("PhonePrefixId không được để trống.");

        RuleFor(v => v.SimStatus)
            .IsInEnum().WithMessage("Trạng thái SIM không hợp lệ.");

        RuleFor(v => v.CategoryIds)
            .Must(ids => ids == null || ids.Count > 0)
            .WithMessage("Danh sách CategoryIds nếu có phải chứa ít nhất 1 mục.");
    }
}
