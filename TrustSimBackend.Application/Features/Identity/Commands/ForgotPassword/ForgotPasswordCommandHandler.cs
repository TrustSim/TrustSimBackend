using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands.ForgotPassword;

/// <summary>
/// Xử lý yêu cầu quên mật khẩu.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ApiResponse<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public ForgotPasswordCommandHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<ApiResponse<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userAccountRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new BadRequestException("Không tìm thấy tài khoản tương ứng với Email này.");
        }

        // Tạo mã xác thực 6 số ngẫu nhiên để đặt lại mật khẩu
        // Trong môi trường production, mã này sẽ được lưu Cache (Redis/Memory) và gửi qua Email Service (SMTP/SendGrid)
        var resetCode = Random.Shared.Next(100000, 999999).ToString();

        return ApiResponse<string>.SuccessResult(
            resetCode,
            $"Mã xác thực đặt lại mật khẩu đã được gửi đến {normalizedEmail}. (Trong môi trường Dev, mã là: {resetCode})");
    }
}
