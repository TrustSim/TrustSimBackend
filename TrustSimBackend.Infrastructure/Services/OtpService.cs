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
    private readonly IEmailService _emailService;
    private readonly SpeedSmsSettings _settings;
    private readonly ILogger<OtpService> _logger;

    public OtpService(
        IMemoryCache cache,
        ISpeedSmsService speedSmsService,
        IEmailService emailService,
        IOptions<SpeedSmsSettings> options,
        ILogger<OtpService> logger)
    {
        _cache = cache;
        _speedSmsService = speedSmsService;
        _emailService = emailService;
        _settings = options.Value;
        _logger = logger;
    }

    // ─── Phone OTP ────────────────────────────────────────────────────────────

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

        _logger.LogInformation(">>> [TrustSim OTP] Sinh mã OTP SMS cho {Phone}: {OtpCode} (Mục đích: {Purpose})", normalizedPhone, otpCode, purpose);

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
        var rawPhone = phoneNumber.Trim().Replace(" ", "");
        var cacheKey = GetOtpCacheKey(normalizedPhone, purpose);

        _logger.LogInformation(
            "[OtpService] Xác thực OTP cho SĐT '{Phone}' (Normalized: '{Normalized}'), Mã '{Otp}', Mục đích '{Purpose}'",
            phoneNumber, normalizedPhone, otpCode, purpose);

        // 1. Thử lấy từ cache key chính, nếu không có thì thử các fallback key phòng trường hợp lệch Purpose / Format SĐT
        if (!_cache.TryGetValue<OtpCacheEntry>(cacheKey, out var entry) || entry == null)
        {
            var fallbackKeys = new[]
            {
                GetOtpCacheKey(normalizedPhone, OtpPurpose.Registration),
                GetOtpCacheKey(normalizedPhone, OtpPurpose.PhoneVerification),
                GetOtpCacheKey(rawPhone, purpose),
                GetOtpCacheKey(rawPhone, OtpPurpose.Registration),
                GetOtpCacheKey(rawPhone, OtpPurpose.PhoneVerification),
            };

            foreach (var key in fallbackKeys)
            {
                if (_cache.TryGetValue<OtpCacheEntry>(key, out var fallbackEntry) && fallbackEntry != null)
                {
                    cacheKey = key;
                    entry = fallbackEntry;
                    _logger.LogInformation("[OtpService] Tìm thấy OTP qua fallback key: '{Key}'", key);
                    break;
                }
            }
        }

        if (entry == null)
        {
            _logger.LogWarning("[OtpService] Không tìm thấy cache OTP cho SĐT '{Normalized}', Key '{Key}'", normalizedPhone, cacheKey);
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

    private static string GetEmailOtpCacheKey(string email, string purpose)
        => $"otp:email:{purpose}:{email}";

    private static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
        if (clean.StartsWith("+84")) clean = "84" + clean[3..];
        else if (clean.StartsWith("0")) clean = "84" + clean[1..];
        return clean;
    }

    // ─── Email OTP ────────────────────────────────────────────────────────────

    public async Task<SendOtpResponseDto> GenerateAndSendEmailOtpAsync(
        string email,
        string purpose,
        int expiryMinutes = 5,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var cacheKey = GetEmailOtpCacheKey(normalizedEmail, purpose);

        const int cooldownSec = 60;
        if (_cache.TryGetValue<OtpCacheEntry>(cacheKey, out var existing) && existing != null)
        {
            var now = DateTimeOffset.UtcNow;
            if (now < existing.CooldownUntil)
            {
                var remaining = (int)Math.Ceiling((existing.CooldownUntil - now).TotalSeconds);
                return new SendOtpResponseDto
                {
                    IsSuccess = false,
                    Message = $"Vui lòng đợi {remaining} giây trước khi yêu cầu gửi lại mã OTP.",
                    CooldownSeconds = remaining,
                    ExpiryMinutes = expiryMinutes
                };
            }
        }

        var otpCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var hashedOtp = HashEmailOtp(otpCode, normalizedEmail);
        var expiryTime = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes);
        var cooldownTime = DateTimeOffset.UtcNow.AddSeconds(cooldownSec);

        _cache.Set(cacheKey, new OtpCacheEntry
        {
            HashedOtp = hashedOtp,
            FailedAttempts = 0,
            ExpiresAt = expiryTime,
            CooldownUntil = cooldownTime,
            IsConsumed = false
        }, expiryTime);

        _logger.LogInformation(">>> [TrustSim Email OTP] Sinh mã OTP cho {Email}: {OtpCode} (Mục đích: {Purpose})", normalizedEmail, otpCode, purpose);

        var sent = await _emailService.SendOtpEmailAsync(normalizedEmail, otpCode, purpose, expiryMinutes, cancellationToken);
        if (!sent)
        {
            _cache.Remove(cacheKey);
            _logger.LogError("Không thể gửi Email OTP tới {Email}", normalizedEmail);
            return new SendOtpResponseDto
            {
                IsSuccess = false,
                Message = "Không thể gửi email xác thực. Vui lòng kiểm tra địa chỉ email và thử lại.",
                CooldownSeconds = 0,
                ExpiryMinutes = expiryMinutes
            };
        }

        _logger.LogInformation("Đã gửi Email OTP tới {Email} cho mục đích {Purpose}", normalizedEmail, purpose);
        return new SendOtpResponseDto
        {
            IsSuccess = true,
            Message = "Mã xác thực OTP đã được gửi tới email của bạn. Vui lòng kiểm tra hộp thư (kể cả Spam).",
            CooldownSeconds = cooldownSec,
            ExpiryMinutes = expiryMinutes
        };
    }

    public Task<VerifyOtpResponseDto> VerifyEmailOtpAsync(
        string email,
        string otpCode,
        string purpose,
        bool consumeOnSuccess = true)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var cacheKey = GetEmailOtpCacheKey(normalizedEmail, purpose);

        if (!_cache.TryGetValue<OtpCacheEntry>(cacheKey, out var entry) || entry == null)
            return Task.FromResult(new VerifyOtpResponseDto { IsValid = false, Message = "Mã OTP không tồn tại hoặc đã hết hạn.", Email = normalizedEmail });

        if (entry.IsConsumed)
            return Task.FromResult(new VerifyOtpResponseDto { IsValid = false, Message = "Mã OTP này đã được sử dụng.", Email = normalizedEmail });

        if (DateTimeOffset.UtcNow > entry.ExpiresAt)
        {
            _cache.Remove(cacheKey);
            return Task.FromResult(new VerifyOtpResponseDto { IsValid = false, Message = "Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.", Email = normalizedEmail });
        }

        if (entry.FailedAttempts >= _settings.MaxFailedAttempts)
        {
            _cache.Remove(cacheKey);
            return Task.FromResult(new VerifyOtpResponseDto { IsValid = false, Message = $"Bạn đã nhập sai OTP quá {_settings.MaxFailedAttempts} lần. Mã đã bị hủy.", Email = normalizedEmail });
        }

        var incomingHash = HashEmailOtp(otpCode.Trim(), normalizedEmail);
        if (!string.Equals(entry.HashedOtp, incomingHash, StringComparison.Ordinal))
        {
            entry.FailedAttempts++;
            var remaining = _settings.MaxFailedAttempts - entry.FailedAttempts;
            _cache.Set(cacheKey, entry, entry.ExpiresAt);
            return Task.FromResult(new VerifyOtpResponseDto
            {
                IsValid = false,
                Message = remaining > 0 ? $"Mã OTP không chính xác. Bạn còn {remaining} lần thử." : "Bạn đã hết số lần thử.",
                Email = normalizedEmail
            });
        }

        if (consumeOnSuccess) { entry.IsConsumed = true; _cache.Remove(cacheKey); }
        _logger.LogInformation("Xác thực Email OTP thành công cho {Email} mục đích {Purpose}", normalizedEmail, purpose);
        return Task.FromResult(new VerifyOtpResponseDto { IsValid = true, Message = "Xác thực email thành công.", Email = normalizedEmail });
    }

    private string HashEmailOtp(string otpCode, string email)
    {
        var rawKey = $"{email}:{otpCode}:{_settings.OtpHashingSecret}";
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes);
    }
}
