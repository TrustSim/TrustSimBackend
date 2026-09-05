using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý yêu cầu quên mật khẩu.
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, string>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public ForgotPasswordCommandHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<string> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userAccountRepository.GetByEmailAsync(normalizedEmail, cancellationToken);

        if (user == null)
        {
            throw new BadRequestException("Không tìm thấy tài khoản tương ứng với Email này.");
        }

        var resetCode = Random.Shared.Next(100000, 999999).ToString();
        return resetCode;
    }
}
