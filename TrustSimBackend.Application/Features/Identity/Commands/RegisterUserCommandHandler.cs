using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Xử lý nghiệp vụ đăng ký tài khoản người dùng mới (Bước 1):
/// 1. Kiểm tra tính duy nhất (Username, Email) qua UserManager.
/// 2. Lưu thông tin đăng ký tạm thời vào MemoryCache (TTL: 10 phút).
/// 3. Sinh và gửi mã Email OTP tới địa chỉ email của người dùng.
/// 4. Chưa tạo bất kỳ bản ghi nào trong Database cho đến khi Verify Email thành công ở Bước 2.
/// </summary>
public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, RegisterInitiatedDto>
{
    private readonly UserManager<UserAccount> _userManager;
    private readonly IOtpService _otpService;
    private readonly IMemoryCache _cache;

    public RegisterUserCommandHandler(
        UserManager<UserAccount> userManager,
        IOtpService otpService,
        IMemoryCache cache)
    {
        _userManager = userManager;
        _otpService = otpService;
        _cache = cache;
    }

    public async Task<RegisterInitiatedDto> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var normalizedUserName = request.UserName.Trim();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Kiểm tra tính duy nhất của Username
        var existingByUserName = await _userManager.FindByNameAsync(normalizedUserName);
        if (existingByUserName != null)
        {
            throw new BadRequestException($"Tên đăng nhập '{normalizedUserName}' đã được sử dụng.");
        }

        // 2. Kiểm tra tính duy nhất của Email
        var existingByEmail = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingByEmail != null)
        {
            throw new BadRequestException($"Email '{normalizedEmail}' đã được sử dụng.");
        }

        // 3. Lưu thông tin đăng ký tạm thời vào MemoryCache (TTL 10 phút)
        var pendingRegistration = new PendingRegistrationModel
        {
            UserName = normalizedUserName,
            Email = normalizedEmail,
            Password = request.Password,
            CreatedAtUtc = DateTime.UtcNow
        };

        var cacheKey = $"registration:pending:{normalizedEmail}";
        _cache.Set(cacheKey, pendingRegistration, TimeSpan.FromMinutes(10));

        // 4. Sinh và gửi mã OTP qua Email
        var otpResult = await _otpService.GenerateAndSendEmailOtpAsync(
            normalizedEmail,
            OtpPurpose.Registration.ToString(),
            expiryMinutes: 10,
            cancellationToken: cancellationToken);

        if (!otpResult.IsSuccess)
        {
            _cache.Remove(cacheKey);
            throw new BadRequestException(otpResult.Message);
        }

        // 5. Trả về thông báo khởi tạo đăng ký thành công
        return new RegisterInitiatedDto
        {
            Email = normalizedEmail,
            Message = "Mã xác thực OTP đã được gửi tới email của bạn. Vui lòng nhập mã OTP để hoàn tất đăng ký tài khoản.",
            CooldownSeconds = otpResult.CooldownSeconds,
            ExpiryMinutes = otpResult.ExpiryMinutes
        };
    }
}
