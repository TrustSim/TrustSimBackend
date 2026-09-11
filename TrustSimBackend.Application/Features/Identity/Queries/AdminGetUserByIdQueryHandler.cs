using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

public class AdminGetUserByIdQueryHandler : IRequestHandler<AdminGetUserByIdQuery, AdminUserDetailDto>
{
    private readonly IUserAccountRepository _userRepository;

    public AdminGetUserByIdQueryHandler(IUserAccountRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminUserDetailDto> Handle(AdminGetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"Không tìm thấy người dùng với ID '{request.Id}'.");

        return new AdminUserDetailDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            Role = user.Role,
            IsActive = !user.IsDeleted,
            FullName = user.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = user.UserProfile?.PhoneNumber ?? string.Empty,
            Address = user.UserProfile?.Address ?? string.Empty,
            IdentityNumber = user.UserProfile?.IdentityNumber ?? string.Empty,
            DateOfBirth = user.UserProfile?.DateOfBirth,
            AvatarImg = user.UserProfile?.AvatarImg ?? string.Empty,
            VerificationStatus = user.UserProfile?.VerificationStatus ?? Domain.Enums.VerificationStatus.PENDING_VERIFICATION,
            AvailableBalance = user.Wallet?.AvailableBalance ?? 0m,
            LockedBalance = user.Wallet?.LockedBalance ?? 0m,
            HasPkiKeys = user.UserSecurityProfile != null,
            CreatedDate = user.CreatedDate
        };
    }
}
