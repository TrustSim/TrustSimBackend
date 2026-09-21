using MediatR;
using Microsoft.AspNetCore.Identity;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

public class AdminCreateUserCommandHandler : IRequestHandler<AdminCreateUserCommand, AdminUserDetailDto>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IUnitOfWork _unitOfWork;

    public AdminCreateUserCommandHandler(
        UserManager<UserAccount> userManager,
        IUnitOfWork unitOfWork)
    {
        _userManager = userManager;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminUserDetailDto> Handle(AdminCreateUserCommand request, CancellationToken cancellationToken)
    {
        var existing = await _userManager.FindByNameAsync(request.UserName);
        if (existing != null) throw new BadRequestException($"Tên đăng nhập '{request.UserName}' đã được sử dụng.");

        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail != null) throw new BadRequestException($"Email '{request.Email}' đã được sử dụng.");

        var userId = Guid.NewGuid();
        var newUser = new UserAccount
        {
            Id = userId,
            UserName = request.UserName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            EmailConfirmed = true,
            Role = request.Role,
            IsDeleted = false,
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        var userProfile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserAccountId = userId,
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber.Trim(),
            IdentityNumber = request.IdentityNumber?.Trim() ?? string.Empty,
            DateOfBirth = request.DateOfBirth ?? new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Address = request.Address?.Trim() ?? string.Empty,
            AvatarImg = string.Empty,
            VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
            CreatedDate = DateTime.UtcNow,
            IsActive = true
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = userId,
            AvailableBalance = 0m,
            LockedBalance = 0m,
            WalletStatus = WalletStatus.ACTIVE,
            CreatedDate = DateTime.UtcNow,
            IsActive = true
        };

        newUser.UserProfile = userProfile;
        newUser.Wallet = wallet;

        var createResult = await _userManager.CreateAsync(newUser, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            throw new BadRequestException($"Tạo tài khoản thất bại: {errors}");
        }

        await _userManager.AddToRoleAsync(newUser, request.Role.ToString());

        return new AdminUserDetailDto
        {
            Id = newUser.Id,
            UserName = newUser.UserName ?? string.Empty,
            Email = newUser.Email ?? string.Empty,
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
