using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ tích hợp cổng thanh toán SePay: sinh VietQR, kiểm tra Token Webhook, regex trích xuất mã giao dịch.
/// </summary>
public class SePayService : ISePayService
{
    private readonly SePaySettings _settings;
    private readonly ILogger<SePayService> _logger;

    public SePayService(IOptions<SePaySettings> options, ILogger<SePayService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public string GeneratePaymentQrUrl(decimal amount, string transactionCode)
    {
        var formattedAmount = Convert.ToInt64(amount);
        var bank = Uri.EscapeDataString(_settings.BankName);
        var acc = Uri.EscapeDataString(_settings.AccountNumber);
        var des = Uri.EscapeDataString(transactionCode);
        var template = Uri.EscapeDataString(_settings.QrTemplate);

        // Format chuẩn theo API sinh VietQR của SePay: https://qr.sepay.vn/img
        return $"https://qr.sepay.vn/img?acc={acc}&bank={bank}&amount={formattedAmount}&des={des}&template={template}";
    }

    public bool ValidateWebhookToken(string? authorizationHeader)
    {
        var expectedToken = !string.IsNullOrWhiteSpace(_settings.WebhookToken)
            ? _settings.WebhookToken
            : _settings.ApiKey;

        // Nếu môi trường dev chưa cấu hình Token, tạm thời cho phép
        if (string.IsNullOrWhiteSpace(expectedToken) || expectedToken.Contains("YOUR_SEPAY"))
        {
            _logger.LogWarning("[SePayService] WebhookToken chưa được cấu hình trong appsettings.json. Tạm thời bỏ qua kiểm tra token trong môi trường Test.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(authorizationHeader))
        {
            return false;
        }

        // SePay gửi header: "Apikey {TOKEN}" hoặc "Bearer {TOKEN}" hoặc token thuần
        var headerValue = authorizationHeader.Trim();
        if (headerValue.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            headerValue = headerValue["Apikey ".Length..].Trim();
        }
        else if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            headerValue = headerValue["Bearer ".Length..].Trim();
        }

        return string.Equals(headerValue, expectedToken, StringComparison.Ordinal);
    }

    public string? ExtractTransactionCode(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var prefix = string.IsNullOrWhiteSpace(_settings.PaymentPrefix) ? "TS" : _settings.PaymentPrefix;
        var pattern = $@"\b({Regex.Escape(prefix)}[0-9A-Za-z]+)\b";
        var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);

        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : null;
    }

    public string GenerateTransactionCode()
    {
        var prefix = string.IsNullOrWhiteSpace(_settings.PaymentPrefix) ? "TS" : _settings.PaymentPrefix;
        var timestamp = DateTime.UtcNow.ToString("yyMMdd");
        var randomPart = Random.Shared.Next(100000, 999999);
        return $"{prefix}{timestamp}{randomPart}";
    }
}
