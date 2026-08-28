using FluentValidation;

namespace TrustSimBackend.Application.Features.Identity.Commands.RegisterUser;

/// <summary>
/// Validator kiểm tra tính hợp lệ của dữ liệu đầu vào khi Đăng ký tài khoản.
/// </summary>
public class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .MinimumLength(3).WithMessage("Tên đăng nhập phải có ít nhất 3 ký tự.")
            .MaximumLength(50).WithMessage("Tên đăng nhập không được vượt quá 50 ký tự.")
            .Matches(@"^[a-zA-Z0-9_]+$").WithMessage("Tên đăng nhập chỉ được chứa chữ cái, số và dấu gạch dưới (_).");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Định dạng email không hợp lệ.")
            .MaximumLength(100).WithMessage("Email không được vượt quá 100 ký tự.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên không được vượt quá 100 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .NotEmpty().WithMessage("Số điện thoại không được để trống.")
            .Matches(@"^(0[3|5|7|8|9])[0-9]{8}$").WithMessage("Số điện thoại không đúng định dạng di động Việt Nam (10 số).");

        RuleFor(x => x.IdentityNumber)
            .NotEmpty().WithMessage("Số CCCD/CMND không được để trống.")
            .Matches(@"^[0-9]{12}$").WithMessage("Số CCCD phải bao gồm chính xác 12 chữ số.");
    }
}
