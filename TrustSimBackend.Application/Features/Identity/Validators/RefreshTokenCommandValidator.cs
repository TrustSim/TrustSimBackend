using FluentValidation;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;

namespace TrustSimBackend.Application.Features.Identity.Validators;

/// <summary>
/// Validator cho RefreshTokenCommand.
/// RefreshToken là tùy chọn (có thể lấy từ Cookie), không bắt buộc validate chặt.
/// </summary>
public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(v => v.RefreshToken)
            .MaximumLength(500).WithMessage("Refresh Token không hợp lệ.")
            .When(v => !string.IsNullOrWhiteSpace(v.RefreshToken));
    }
}
