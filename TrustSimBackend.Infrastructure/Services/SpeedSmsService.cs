using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ giao tiếp với SpeedSMS API qua HttpClient.
/// </summary>
public class SpeedSmsService : ISpeedSmsService
{
    private readonly HttpClient _httpClient;
    private readonly SpeedSmsSettings _settings;
    private readonly ILogger<SpeedSmsService> _logger;

    public SpeedSmsService(
        HttpClient httpClient,
        IOptions<SpeedSmsSettings> options,
        ILogger<SpeedSmsService> logger)
    {
        _settings = options.Value;
        _logger = logger;
        _httpClient = httpClient;

        _httpClient.BaseAddress = new Uri(_settings.RootUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds > 0 ? _settings.TimeoutSeconds : 30);

        // SpeedSMS sử dụng HTTP Basic Authentication với AccessToken:x
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_settings.ApiKey}:x"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    public async Task<SpeedSmsUserInfoResponse?> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("user/info", cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<SpeedSmsUserInfoResponse>(content, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy thông tin tài khoản SpeedSMS: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<SpeedSmsSendResponse> SendSmsAsync(
        string phoneNumber,
        string content,
        SpeedSmsType? type = null,
        string? sender = null,
        CancellationToken cancellationToken = default)
    {
        return await SendSmsBatchAsync(new[] { phoneNumber }, content, type, sender, cancellationToken);
    }

    public async Task<SpeedSmsSendResponse> SendSmsBatchAsync(
        IEnumerable<string> phoneNumbers,
        string content,
        SpeedSmsType? type = null,
        string? sender = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPhones = phoneNumbers
            .Select(NormalizePhoneNumber)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct()
            .ToList();

        if (!normalizedPhones.Any())
        {
            return new SpeedSmsSendResponse
            {
                Status = "error",
                Code = 400,
                Message = "Danh sách số điện thoại không hợp lệ."
            };
        }

        var effectiveType = (int)(type ?? (SpeedSmsType)_settings.SmsType);
        var effectiveSender = sender ?? _settings.Sender ?? "";

        if (effectiveType == 5 && string.IsNullOrWhiteSpace(effectiveSender))
        {
            var msg = "Với phương thức SMS Gateway (type = 5), bạn cần cấu hình 'Sender' là 'Device ID' của thiết bị Android đã liên kết trong mục 'Devices' hoặc trên app SpeedSMS Gateway.";
            _logger.LogWarning(msg);
            return new SpeedSmsSendResponse
            {
                Status = "error",
                Code = "DEVICE_ID_REQUIRED",
                Message = msg
            };
        }

        var payload = new
        {
            to = normalizedPhones,
            content = content,
            type = effectiveType,
            sender = effectiveSender
        };

        try
        {
            var jsonBody = JsonSerializer.Serialize(payload);
            var httpContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("sms/send", httpContent, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);

            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<SpeedSmsSendResponse>(responseString, jsonOptions)
                ?? new SpeedSmsSendResponse { Status = "error", Message = "Phản hồi rỗng từ máy chủ SpeedSMS." };

            if (!result.IsSuccess)
            {
                _logger.LogWarning("Gửi SMS qua SpeedSMS thất bại. Mã: {Code}, Lỗi: {Message}", result.Code, result.Message);
            }
            else
            {
                _logger.LogInformation("Gửi SMS qua SpeedSMS thành công tới {Count} số điện thoại (Loại SMS: {Type}).",
                    normalizedPhones.Count, effectiveType);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi ngoại lệ khi gửi SMS qua SpeedSMS: {Message}", ex.Message);
            return new SpeedSmsSendResponse { Status = "error", Code = 500, Message = ex.Message };
        }
    }

    public async Task<SpeedSmsSendResponse> SendOtpSmsAsync(string phoneNumber, string otpCode, CancellationToken cancellationToken = default)
    {
        var content = $"Ma xac thuc OTP TrustSim cua ban la {otpCode}. Ma co hieu luc trong {_settings.OtpExpiryMinutes} phut. Khong chia se ma nay cho bat ky ai.";
        var smsType = (SpeedSmsType)_settings.SmsType;
        return await SendSmsAsync(phoneNumber, content, smsType, _settings.Sender, cancellationToken);
    }

    /// <summary>
    /// Chuẩn hóa số điện thoại về định dạng quốc tế 84xxxxxxxxx mà SpeedSMS yêu cầu.
    /// </summary>
    private static string NormalizePhoneNumber(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
        var clean = phone.Trim().Replace(" ", "").Replace("-", "").Replace(".", "");
        if (clean.StartsWith("+84")) clean = "84" + clean[3..];
        else if (clean.StartsWith("0")) clean = "84" + clean[1..];
        return clean;
    }
}
