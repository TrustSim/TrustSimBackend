using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

/// <summary>
/// Xử lý truy vấn lấy hồ sơ cá nhân của người dùng.
/// </summary>
public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, UserProfileDto>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public GetUserProfileQueryHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<UserProfileDto> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var user = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        return new UserProfileDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Address = user.UserProfile?.Address ?? string.Empty,
            AvatarImg = user.UserProfile?.AvatarImg ?? string.Empty,
            IdentityNumber = user.UserProfile?.IdentityNumber ?? string.Empty,
            DateOfBirth = user.UserProfile?.DateOfBirth,
            VerificationStatus = user.UserProfile?.VerificationStatus ?? Domain.Enums.VerificationStatus.PENDING_VERIFICATION,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            WalletBalance = user.Wallet?.AvailableBalance ?? 0
        };
    }
}
