using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý cập nhật thông tin cá nhân của người dùng.
/// </summary>
public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, UserProfileDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public UpdateProfileCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<UserProfileDto> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        var normalizedPhone = request.PhoneNumber.Trim();
        var normalizedIdentity = request.IdentityNumber.Trim();

        // 1. Kiểm tra SĐT có bị trùng với tài khoản khác không
        if (await _unitOfWork.Users.ExistsByPhoneNumberAsync(normalizedPhone, request.UserId, cancellationToken))
        {
            throw new BadRequestException($"Số điện thoại '{normalizedPhone}' đã được sử dụng bởi người dùng khác.");
        }

        // 2. Kiểm tra CCCD có bị trùng với tài khoản khác không
        if (await _unitOfWork.Users.ExistsByIdentityNumberAsync(normalizedIdentity, request.UserId, cancellationToken))
        {
            throw new BadRequestException($"Số CCCD/CMND '{normalizedIdentity}' đã được sử dụng bởi người dùng khác.");
        }

        // 3. Khởi tạo Profile nếu chưa có
        if (user.UserProfile == null)
        {
            user.UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = user.Id,
                Email = user.UserEmail
            };
        }

        // 4. Cập nhật thông tin
        user.UserProfile.FullName = request.FullName.Trim();
        user.UserProfile.PhoneNumber = normalizedPhone;
        user.UserProfile.IdentityNumber = normalizedIdentity;
        user.UserProfile.Address = request.Address.Trim();
        if (request.DateOfBirth.HasValue)
        {
            user.UserProfile.DateOfBirth = DateTime.SpecifyKind(request.DateOfBirth.Value, DateTimeKind.Utc);
        }

        _unitOfWork.Users.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Trả về DTO sau khi cập nhật
        return new UserProfileDto
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.UserEmail,
            FullName = user.UserProfile.FullName,
            PhoneNumber = user.UserProfile.PhoneNumber,
            Address = user.UserProfile.Address,
            AvatarImg = user.UserProfile.AvatarImg,
            IdentityNumber = user.UserProfile.IdentityNumber,
            DateOfBirth = user.UserProfile.DateOfBirth,
            VerificationStatus = user.UserProfile.VerificationStatus,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            WalletBalance = user.Wallet?.AvailableBalance ?? 0
        };
    }
}
