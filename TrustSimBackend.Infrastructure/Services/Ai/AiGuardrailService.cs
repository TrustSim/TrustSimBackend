using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Provides input, tool-argument, and output guardrails for the AI consultation pipeline.
/// </summary>
public class AiGuardrailService : IAiGuardrailService
{
    private readonly GeminiSettings _settings;
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<AiGuardrailService> _logger;

    private static readonly Regex[] InjectionPatterns =
    {
        new(@"(?i)\bignore\s+(all\s+|any\s+|previous\s+|prior\s+|above\s+)?(instructions?|rules?|directives?|prompts?)\b", RegexOptions.Compiled),
        new(@"(?i)\bdisregard\s+(all\s+|previous\s+|the\s+)?(system|instructions?|rules?|prompt)\b", RegexOptions.Compiled),
        new(@"(?i)\b(reveal|show|print|output|dump|repeat|leak)\s+(your\s+)?(system\s+prompt|hidden\s+instructions?|internal\s+prompt|api\s*key|connection\s*string|secret)\b", RegexOptions.Compiled),
        new(@"(?i)\b(jailbreak|DAN\s+mode|developer\s+mode|god\s+mode|bypass\s+(guardrail|safety|policy))\b", RegexOptions.Compiled),
        new(@"(?i)\b(bỏ\s+qua|quên\s+hết|phớt\s+lờ)\s+(mọi|toàn\s+bộ|các)?\s*(hướng\s+dẫn|quy\s+tắc|chỉ\s+thị|ràng\s+buộc)\b", RegexOptions.Compiled),
        new(@"(?i)\b(tiết\s+lộ|cho\s+xem|hiển\s+thị|in\s+ra|xuất\s+ra)\s+(system\s+prompt|prompt\s+hệ\s+thống|chỉ\s+thị\s+gốc|api\s*key|mật\s+khẩu|chuỗi\s+kết\s+nối)\b", RegexOptions.Compiled),
        new(@"(?i)<\s*script\b|javascript\s*:|union\s+all\s+select|drop\s+table\b", RegexOptions.Compiled)
    };

    private static readonly Regex[] SecretRedactionPatterns =
    {
        new(@"AIzaSy[A-Za-z0-9_\-]{18,}", RegexOptions.Compiled),
        new(@"eyJ[A-Za-z0-9_\-]{10,}\.eyJ[A-Za-z0-9_\-]{10,}\.[A-Za-z0-9_\-]{10,}", RegexOptions.Compiled),
        new(@"(?i)(Password|Pwd|SecretKey|ApiKey)\s*=\s*[^;\s""']+", RegexOptions.Compiled),
        new(@"(?i)postgres(?:ql)?://[^\s""']+", RegexOptions.Compiled)
    };

    private static readonly Regex PhoneCandidateRegex = new(@"\b0[35789](?:[.\-\s]?\d){8}\b", RegexOptions.Compiled);

    public AiGuardrailService(
        IOptions<GeminiSettings> settings,
        IMemoryCache memoryCache,
        ILogger<AiGuardrailService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _settings = settings.Value;
        _memoryCache = memoryCache;
        _logger = logger;
        _redis = redis;
    }

    public async Task<AiInputGuardrailResult> EvaluateInputAsync(
        string message,
        AiUserContext userContext,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new AiInputGuardrailResult
            {
                IsAllowed = false,
                RejectionReason = "EMPTY_MESSAGE",
                SafeReplyContent = "Nội dung tin nhắn không được để trống."
            };
        }

        var trimmed = message.Trim();
        var maxLen = _settings.MaxMessageLength > 0 ? _settings.MaxMessageLength : 2000;
        if (trimmed.Length > maxLen)
        {
            return new AiInputGuardrailResult
            {
                IsAllowed = false,
                IsLengthExceeded = true,
                RejectionReason = $"MESSAGE_LENGTH_EXCEEDED:{trimmed.Length}>{maxLen}",
                SafeReplyContent = $"Nội dung tin nhắn vượt quá giới hạn tối đa {maxLen} ký tự."
            };
        }

        if (await IsRateLimitExceededAsync(userContext))
        {
            _logger.LogWarning("[AI Guardrail] Rate limit exceeded for {Identity}", userContext.RateLimitIdentity);
            return new AiInputGuardrailResult
            {
                IsAllowed = false,
                IsRateLimited = true,
                RejectionReason = "RATE_LIMIT_EXCEEDED",
                SafeReplyContent = "Bạn đang gửi yêu cầu quá nhanh. Vui lòng đợi ít giây trước khi tiếp tục trò chuyện với Chuyên gia AI TrustSim."
            };
        }

        foreach (var pattern in InjectionPatterns)
        {
            if (pattern.IsMatch(trimmed))
            {
                _logger.LogWarning("[AI Guardrail] Prompt injection / unsafe payload blocked for {Identity}", userContext.RateLimitIdentity);
                return new AiInputGuardrailResult
                {
                    IsAllowed = false,
                    IsInjectionDetected = true,
                    RejectionReason = $"PROMPT_INJECTION_MATCH:{pattern}",
                    SafeReplyContent = "Xin lỗi, tôi chỉ hỗ trợ tư vấn SIM số đẹp, định giá, phong thủy, đấu giá và quy trình giao dịch an toàn tại sàn TrustSim. Tôi không thể thực hiện yêu cầu thay đổi quy tắc hệ thống hoặc tiết lộ cấu hình nội bộ."
                };
            }
        }

        return new AiInputGuardrailResult
        {
            IsAllowed = true,
            SanitizedMessage = StripControlChars(trimmed)
        };
    }

    public (bool IsValid, string? ErrorMessage) ValidateToolArguments(string toolName, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object && arguments.ValueKind != JsonValueKind.Undefined)
        {
            return (false, "Tham số công cụ phải là JSON object.");
        }

        if (arguments.ValueKind == JsonValueKind.Undefined)
        {
            return (true, null);
        }

        decimal? minPrice = TryGetDecimal(arguments, "minPrice");
        decimal? maxPrice = TryGetDecimal(arguments, "maxPrice");
        if (minPrice < 0 || maxPrice < 0)
        {
            return (false, "Ngân sách tìm kiếm SIM không được âm.");
        }
        if (minPrice.HasValue && maxPrice.HasValue && minPrice.Value > maxPrice.Value)
        {
            return (false, "Giá tối thiểu (minPrice) không được lớn hơn giá tối đa (maxPrice).");
        }

        decimal? price = TryGetDecimal(arguments, "price");
        if (price.HasValue && (price.Value <= 0 || price.Value > 1_000_000_000_000m))
        {
            return (false, "Giá trị giao dịch SIM không hợp lệ.");
        }

        decimal? amount = TryGetDecimal(arguments, "amount");
        if (amount.HasValue && (amount.Value <= 0 || amount.Value > 100_000_000_000m))
        {
            return (false, "Số tiền nạp ví phải lớn hơn 0 và trong hạn mức cho phép.");
        }

        if (arguments.TryGetProperty("birthYear", out var yearEl) && yearEl.ValueKind == JsonValueKind.Number)
        {
            var year = yearEl.GetInt32();
            if (year < 1920 || year > 2030)
            {
                return (false, "Năm sinh tra cứu phong thủy phải nằm trong khoảng 1920 đến 2030.");
            }
        }

        foreach (var prop in arguments.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var str = prop.Value.GetString() ?? string.Empty;
                if (str.Length > 200)
                {
                    return (false, $"Tham số '{prop.Name}' vượt quá độ dài cho phép (200 ký tự).");
                }
                if (InjectionPatterns.Any(p => p.IsMatch(str)))
                {
                    return (false, $"Tham số '{prop.Name}' chứa nội dung không hợp lệ.");
                }
            }
        }

        return (true, null);
    }

    public AiOutputGuardrailResult EvaluateAndSanitizeOutput(
        string content,
        string userMessage,
        IReadOnlyList<ConsultationSimSuggestion> toolSims)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new AiOutputGuardrailResult { SanitizedContent = string.Empty };
        }

        var sanitized = content;
        int redactionCount = 0;

        foreach (var regex in SecretRedactionPatterns)
        {
            var matches = regex.Matches(sanitized);
            if (matches.Count > 0)
            {
                redactionCount += matches.Count;
                sanitized = regex.Replace(sanitized, "[REDACTED]");
            }
        }

        var knownPhones = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddNormalizedPhones(knownPhones, userMessage);
        foreach (var sim in toolSims)
        {
            AddNormalizedPhones(knownPhones, sim.PhoneNumber);
        }

        var suspiciousPhones = new List<string>();
        foreach (Match match in PhoneCandidateRegex.Matches(sanitized))
        {
            var digits = NormalizePhoneDigits(match.Value);
            if (digits.Length == 10 && !knownPhones.Contains(digits) && !suspiciousPhones.Contains(digits))
            {
                suspiciousPhones.Add(digits);
            }
        }

        return new AiOutputGuardrailResult
        {
            SanitizedContent = sanitized,
            WasRedacted = redactionCount > 0,
            RedactionCount = redactionCount,
            HasPotentialHallucination = suspiciousPhones.Count > 0,
            SuspiciousPhoneNumbers = suspiciousPhones
        };
    }

    private async Task<bool> IsRateLimitExceededAsync(AiUserContext userContext)
    {
        var limit = _settings.RateLimitRequestsPerMinute;
        if (limit <= 0)
        {
            return false;
        }

        var windowBucket = DateTime.UtcNow.ToString("yyyyMMddHHmm");
        var key = $"trustsim:ai:ratelimit:{userContext.RateLimitIdentity}:{windowBucket}";

        if (_redis is { IsConnected: true })
        {
            try
            {
                var db = _redis.GetDatabase();
                var count = await db.StringIncrementAsync(key);
                if (count == 1)
                {
                    await db.KeyExpireAsync(key, TimeSpan.FromMinutes(2));
                }
                return count > limit;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[AI Guardrail] Redis rate-limit check failed, using L1 memory counter.");
            }
        }

        var current = _memoryCache.Get<int>(key) + 1;
        _memoryCache.Set(key, current, TimeSpan.FromMinutes(2));
        return current > limit;
    }

    private static decimal? TryGetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return prop.TryGetDecimal(out var val) ? val : null;
    }

    private static string StripControlChars(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            if (!char.IsControl(ch) || ch is '\n' or '\r' or '\t')
            {
                sb.Append(ch);
            }
        }
        return sb.ToString();
    }

    private static void AddNormalizedPhones(HashSet<string> set, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        foreach (Match match in PhoneCandidateRegex.Matches(text))
        {
            var digits = NormalizePhoneDigits(match.Value);
            if (digits.Length == 10)
            {
                set.Add(digits);
            }
        }
    }

    private static string NormalizePhoneDigits(string value)
    {
        var sb = new StringBuilder(10);
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
