using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

/// <summary>
/// Validator cho LogoutCommand.
/// RefreshToken là tùy chọn (có thể lấy từ Cookie), không bắt buộc validate chặt.
/// </summary>
public class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
        RuleFor(v => v.RefreshToken)
            .MaximumLength(500).WithMessage("Refresh Token không hợp lệ.")
            .When(v => !string.IsNullOrWhiteSpace(v.RefreshToken));
    }
}
