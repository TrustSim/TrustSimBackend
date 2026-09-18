using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

public class AdminUpdateUserCommandHandler : IRequestHandler<AdminUpdateUserCommand, AdminUserDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminUpdateUserCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminUserDetailDto> Handle(AdminUpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.Id, cancellationToken);
        if (user == null) throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.Id}'.");

        user.UserEmail = request.Email.Trim().ToLower();
        user.Role = request.Role;
        user.UpdatedDate = DateTime.UtcNow;

        if (user.UserProfile == null)
        {
            user.UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = user.Id,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim().ToLower(),
                PhoneNumber = request.PhoneNumber.Trim(),
                IdentityNumber = request.IdentityNumber?.Trim() ?? string.Empty,
                DateOfBirth = request.DateOfBirth ?? new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Address = request.Address?.Trim() ?? string.Empty,
                VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
                CreatedDate = DateTime.UtcNow
            };
        }
        else
        {
            user.UserProfile.FullName = request.FullName.Trim();
            user.UserProfile.Email = request.Email.Trim().ToLower();
            user.UserProfile.PhoneNumber = request.PhoneNumber.Trim();
            user.UserProfile.IdentityNumber = request.IdentityNumber?.Trim() ?? string.Empty;
            if (request.DateOfBirth.HasValue) user.UserProfile.DateOfBirth = request.DateOfBirth.Value;
            user.UserProfile.Address = request.Address?.Trim() ?? string.Empty;
            user.UserProfile.UpdatedDate = DateTime.UtcNow;
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminUserDetailDto
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            Role = user.Role,
            IsActive = !user.IsDeleted,
            FullName = user.UserProfile.FullName,
            PhoneNumber = user.UserProfile.PhoneNumber,
            Address = user.UserProfile.Address,
            IdentityNumber = user.UserProfile.IdentityNumber,
            DateOfBirth = user.UserProfile.DateOfBirth,
            AvatarImg = user.UserProfile.AvatarImg,
            VerificationStatus = user.UserProfile.VerificationStatus,
            AvailableBalance = user.Wallet?.AvailableBalance ?? 0m,
            LockedBalance = user.Wallet?.LockedBalance ?? 0m,
            HasPkiKeys = user.UserSecurityProfile != null,
            CreatedDate = user.CreatedDate
        };
    }
}
