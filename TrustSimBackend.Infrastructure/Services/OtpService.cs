using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ quản lý vòng đời OTP (sinh số ngẫu nhiên mật mã học, băm bảo mật, kiểm soát Cooldown, Rate Limit và xác thực).
/// </summary>
public class OtpService : IOtpService
{
    private readonly IMemoryCache _cache;
    private readonly ISpeedSmsService _speedSmsService;
    private readonly SpeedSmsSettings _settings;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IMemoryCache cache,
        ISpeedSmsService speedSmsService,
        IOptions<SpeedSmsSettings> options,
        ILogger<OtpService> logger)
    {
        _cache = cache;
        _speedSmsService = speedSmsService;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<SendOtpResponseDto> GenerateAndSendPhoneOtpAsync(
        string phoneNumber,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        var cacheKey = GetOtpCacheKey(normalizedPhone, purpose);

        // 1. Kiểm tra Cooldown (chống spam SMS Bombing)
        if (_cache.TryGetValue<OtpCacheEntry>(cacheKey, out var existingEntry) && existingEntry != null)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < existingEntry.CooldownUntil)
            {
                var remainingSeconds = (int)Math.Ceiling((existingEntry.CooldownUntil - now).TotalSeconds);
                return new SendOtpResponseDto
                {
                    IsSuccess = false,
                    Message = $"Vui lòng đợi {remainingSeconds} giây trước khi yêu cầu gửi lại mã OTP.",
                    CooldownSeconds = remainingSeconds,
                    ExpiryMinutes = _settings.OtpExpiryMinutes
                };
            }
        }

        // 2. Sinh mã OTP 6 chữ số bằng thuật toán ngẫu nhiên mật mã học
        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hashedOtp = HashOtp(otpCode, normalizedPhone);

        var expiryTime = DateTimeOffset.UtcNow.AddMinutes(_settings.OtpExpiryMinutes);
        var cooldownTime = DateTimeOffset.UtcNow.AddSeconds(_settings.CooldownSeconds);

        var newEntry = new OtpCacheEntry
        {
            HashedOtp = hashedOtp,
            FailedAttempts = 0,
            ExpiresAt = expiryTime,
            CooldownUntil = cooldownTime,
            IsConsumed = false
        };

        // Lưu vào MemoryCache với thời gian hết hạn tương ứng
        _cache.Set(cacheKey, newEntry, expiryTime);

        // 3. Gửi SMS OTP qua SpeedSMS
        var smsResponse = await _speedSmsService.SendOtpSmsAsync(normalizedPhone, otpCode, cancellationToken);

        if (!smsResponse.IsSuccess)
        {
            _logger.LogError("Không thể gửi tin nhắn OTP qua SpeedSMS tới {Phone}: {Error}", normalizedPhone, smsResponse.Message);
            return new SendOtpResponseDto
            {
                IsSuccess = false,
                Message = $"Không thể gửi SMS OTP: {smsResponse.Message ?? "Lỗi kết nối cổng SMS"}",
                CooldownSeconds = 0,
                ExpiryMinutes = _settings.OtpExpiryMinutes
            };
        }

        _logger.LogInformation("Đã gửi mã OTP thành công tới {Phone} cho mục đích {Purpose}", normalizedPhone, purpose);

        return new SendOtpResponseDto
        {
            IsSuccess = true,
            Message = "Mã xác thực OTP đã được gửi tới số điện thoại của bạn.",
            CooldownSeconds = _settings.CooldownSeconds,
            ExpiryMinutes = _settings.OtpExpiryMinutes
        };
    }

    public Task<VerifyOtpResponseDto> VerifyPhoneOtpAsync(
        string phoneNumber,
        string otpCode,
        OtpPurpose purpose,
        bool consumeOnSuccess = true)
    {
        var normalizedPhone = NormalizePhoneNumber(phoneNumber);
        var cacheKey = GetOtpCacheKey(normalizedPhone, purpose);

        if (!_cache.TryGetValue<OtpCacheEntry>(cacheKey, out var entry) || entry == null)
        {
            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = "Mã OTP không tồn tại hoặc đã hết hạn. Vui lòng yêu cầu mã mới.",
                PhoneNumber = normalizedPhone,
                Purpose = purpose
            });
        }

        if (entry.IsConsumed)
        {
            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = "Mã OTP này đã được sử dụng trước đó.",
                PhoneNumber = normalizedPhone,
                Purpose = purpose
            });
        }

        if (DateTimeOffset.UtcNow > entry.ExpiresAt)
        {
            _cache.Remove(cacheKey);
            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = "Mã OTP đã hết hạn sử dụng. Vui lòng yêu cầu mã mới.",
                PhoneNumber = normalizedPhone,
                Purpose = purpose
            });
        }

        // Kiểm tra số lần nhập sai
        if (entry.FailedAttempts >= _settings.MaxFailedAttempts)
        {
            _cache.Remove(cacheKey);
            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = $"Bạn đã nhập sai mã OTP quá {_settings.MaxFailedAttempts} lần. Mã đã bị hủy để đảm bảo an toàn.",
                PhoneNumber = normalizedPhone,
                Purpose = purpose
            });
        }

        var incomingHash = HashOtp(otpCode.Trim(), normalizedPhone);

        if (!string.Equals(entry.HashedOtp, incomingHash, StringComparison.Ordinal))
        {
            entry.FailedAttempts++;
            var remainingAttempts = _settings.MaxFailedAttempts - entry.FailedAttempts;

            if (remainingAttempts <= 0)
            {
                _cache.Remove(cacheKey);
                return Task.FromResult(new VerifyOtpResponseDto
                {
                    IsValid = false,
                    Message = "Mã OTP không chính xác. Bạn đã hết số lần thử lại.",
                    PhoneNumber = normalizedPhone,
                    Purpose = purpose
                });
            }

            // Cập nhật lại số lần sai vào cache
            _cache.Set(cacheKey, entry, entry.ExpiresAt);

            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = $"Mã OTP không chính xác. Bạn còn {remainingAttempts} lần thử.",
                PhoneNumber = normalizedPhone,
                Purpose = purpose
            });
        }

        // Xác thực thành công
        if (consumeOnSuccess)
        {
            entry.IsConsumed = true;
            _cache.Remove(cacheKey);
        }

        _logger.LogInformation("Xác thực OTP thành công cho số {Phone} mục đích {Purpose}", normalizedPhone, purpose);

        return Task.FromResult(new VerifyOtpResponseDto
        {
            IsValid = true,
            Message = "Xác thực mã OTP thành công.",
            PhoneNumber = normalizedPhone,
            Purpose = purpose
        });
    }

    private string HashOtp(string otpCode, string phoneNumber)
    {
        var rawKey = $"{phoneNumber}:{otpCode}:{_settings.OtpHashingSecret}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes);
    }

    private static string GetOtpCacheKey(string phoneNumber, OtpPurpose purpose)
    {
        return $"otp:{purpose}:{phoneNumber}";
    }

    private static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
        if (clean.StartsWith("+84")) clean = "84" + clean[3..];
        else if (clean.StartsWith("0")) clean = "84" + clean[1..];
        return clean;
    }
}
