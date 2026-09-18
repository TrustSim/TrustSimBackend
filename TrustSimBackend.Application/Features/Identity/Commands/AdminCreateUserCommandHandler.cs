using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

public class AdminCreateUserCommandHandler : IRequestHandler<AdminCreateUserCommand, AdminUserDetailDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;

    public AdminCreateUserCommandHandler(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
    }

    public async Task<AdminUserDetailDto> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _unitOfWork.Users.GetByUserNameAsync(request.UserName, cancellationToken);
        if (existing != null) throw new BadRequestException($"Tên đăng nhập '{request.UserName}' đã được sử dụng.");

        var existingEmail = await _unitOfWork.Users.GetByEmailAsync(request.Email, cancellationToken);
        if (existingEmail != null) throw new BadRequestException($"Email '{request.Email}' đã được sử dụng.");

        var userId = Guid.NewGuid();
        var newUser = new UserAccount
        {
            Id = userId,
            UserName = request.UserName.Trim().ToLower(),
            UserEmail = request.Email.Trim().ToLower(),
            PassWordHash = _passwordHasher.HashPassword(request.Password),
            Role = request.Role,
            IsDeleted = false,
            CreatedDate = DateTime.UtcNow,
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim().ToLower(),
                PhoneNumber = request.PhoneNumber.Trim(),
                IdentityNumber = request.IdentityNumber?.Trim() ?? string.Empty,
                DateOfBirth = request.DateOfBirth ?? new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                Address = request.Address?.Trim() ?? string.Empty,
                AvatarImg = string.Empty,
                VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
                CreatedDate = DateTime.UtcNow
            },
            Wallet = new Wallet
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                AvailableBalance = 0m,
                LockedBalance = 0m,
                WalletStatus = WalletStatus.ACTIVE,
                CreatedDate = DateTime.UtcNow
            }
        };

        await _unitOfWork.Users.AddAsync(newUser, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new AdminUserDetailDto
        {
            Id = newUser.Id,
            UserName = newUser.UserName,
            Email = newUser.UserEmail,
            Role = newUser.Role,
            IsActive = !newUser.IsDeleted,
            FullName = newUser.UserProfile.FullName,
            PhoneNumber = newUser.UserProfile.PhoneNumber,
            Address = newUser.UserProfile.Address,
            IdentityNumber = newUser.UserProfile.IdentityNumber,
            DateOfBirth = newUser.UserProfile.DateOfBirth,
            AvatarImg = newUser.UserProfile.AvatarImg,
            VerificationStatus = newUser.UserProfile.VerificationStatus,
            AvailableBalance = 0m,
            LockedBalance = 0m,
            HasPkiKeys = false,
            CreatedDate = newUser.CreatedDate
        };
    }
}
