using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ gửi email bằng thư viện MailKit và MimeKit.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        var message = new EmailMessage
        {
            To = new List<string> { toEmail },
            Subject = subject,
            HtmlBody = htmlBody
        };
        return await SendEmailAsync(message, cancellationToken);
    }

    public async Task<bool> SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            var mimeMessage = new MimeMessage();
            mimeMessage.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

            foreach (var to in message.To.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mimeMessage.To.Add(MailboxAddress.Parse(to.Trim()));
            }

            foreach (var cc in message.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mimeMessage.Cc.Add(MailboxAddress.Parse(cc.Trim()));
            }

            foreach (var bcc in message.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                mimeMessage.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
            }

            mimeMessage.Subject = message.Subject;

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = message.HtmlBody,
                TextBody = message.PlainTextBody
            };

            if (message.Attachments != null)
            {
                foreach (var att in message.Attachments)
                {
                    bodyBuilder.Attachments.Add(att.FileName, att.Content, ContentType.Parse(att.ContentType));
                }
            }

            mimeMessage.Body = bodyBuilder.ToMessageBody();

            // Dev-Mode Fallback: Nếu chưa cấu hình Username hoặc Password trong appsettings.json, ghi log cảnh báo và bỏ qua gửi SMTP thật
            if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            {
                _logger.LogWarning(">>> [EmailService DEV-MOCK] Chưa cấu hình SMTP Username/Password trong appsettings.json. Bỏ qua gửi SMTP thật. Email gửi tới {Recipients}: Tiêu đề='{Subject}'",
                    string.Join(", ", message.To), message.Subject);
                return true;
            }

            using var client = new SmtpClient();
            client.Timeout = _settings.TimeoutSeconds * 1000;

            var secureSocketOption = _settings.UseStartTls
                ? SecureSocketOptions.StartTls
                : (_settings.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto);

            await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureSocketOption, cancellationToken);

            if (!string.IsNullOrEmpty(_settings.Username) && !string.IsNullOrEmpty(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Gửi email thành công tới {Recipients} với tiêu đề '{Subject}'",
                string.Join(", ", message.To), message.Subject);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email tới {Recipients}: {Message}",
                string.Join(", ", message.To), ex.Message);
            return false;
        }
    }

    public async Task<bool> SendOtpEmailAsync(string toEmail, string otpCode, string purpose = "xác thực tài khoản", int expiryMinutes = 5, CancellationToken cancellationToken = default)
    {
        var subject = $"[TrustSim] Mã OTP {purpose}: {otpCode}";
        var body = GetOtpEmailTemplate(otpCode, purpose, expiryMinutes);
        return await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public async Task<bool> SendNotificationEmailAsync(string toEmail, string title, string message, string? actionUrl = null, string? actionText = null, CancellationToken cancellationToken = default)
    {
        var subject = $"[TrustSim] {title}";
        var body = GetNotificationEmailTemplate(title, message, actionUrl, actionText);
        return await SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    private static string GetOtpEmailTemplate(string otpCode, string purpose, int expiryMinutes)
    {
        return $@"
        <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; border: 1px solid #e2e8f0; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1);"">
            <div style=""background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); padding: 30px 20px; text-align: center;"">
                <h1 style=""color: #ffffff; margin: 0; font-size: 26px; font-weight: 700; letter-spacing: 1px;"">TRUSTSIM</h1>
                <p style=""color: #93c5fd; margin: 6px 0 0 0; font-size: 14px;"">Sàn Giao Dịch & Đấu Giá SIM Số Đẹp An Toàn</p>
            </div>
            <div style=""padding: 30px 25px;"">
                <p style=""font-size: 16px; color: #334155; margin-bottom: 20px;"">Xin chào,</p>
                <p style=""font-size: 15px; color: #475569; line-height: 1.6;"">
                    Bạn vừa thực hiện yêu cầu <strong>{purpose}</strong> trên hệ thống TrustSim. Vui lòng sử dụng mã OTP dưới đây để hoàn tất:
                </p>
                <div style=""margin: 30px 0; text-align: center;"">
                    <div style=""display: inline-block; background-color: #f1f5f9; border: 2px dashed #3b82f6; border-radius: 8px; padding: 15px 35px;"">
                        <span style=""font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #1e3a8a;"">{otpCode}</span>
                    </div>
                    <p style=""color: #ef4444; font-size: 13px; margin-top: 10px; font-weight: 500;"">⏱️ Mã có hiệu lực trong vòng {expiryMinutes} phút.</p>
                </div>
                <p style=""font-size: 13px; color: #64748b; line-height: 1.5;"">
                    ⚠️ <em>Lưu ý an toàn: Không chia sẻ mã xác thực này cho bất kỳ ai, kể cả nhân viên hỗ trợ của TrustSim.</em>
                </p>
            </div>
            <div style=""background-color: #f8fafc; padding: 20px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;"">
                <p style=""margin: 0;"">© 2026 TrustSim Platform. Bảo lưu mọi quyền.</p>
                <p style=""margin: 5px 0 0 0;"">Email tự động, vui lòng không phản hồi trực tiếp vào địa chỉ này.</p>
            </div>
        </div>";
    }

    private static string GetNotificationEmailTemplate(string title, string message, string? actionUrl, string? actionText)
    {
        var actionBtn = !string.IsNullOrEmpty(actionUrl) && !string.IsNullOrEmpty(actionText)
            ? $@"<div style=""text-align: center; margin: 30px 0;"">
                    <a href=""{actionUrl}"" style=""background-color: #2563eb; color: #ffffff; padding: 12px 28px; text-decoration: none; border-radius: 6px; font-weight: 600; font-size: 15px; display: inline-block;"">{actionText}</a>
                 </div>"
            : "";

        return $@"
        <div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; border: 1px solid #e2e8f0;"">
            <div style=""background: linear-gradient(135deg, #1e3a8a 0%, #3b82f6 100%); padding: 25px 20px; text-align: center;"">
                <h1 style=""color: #ffffff; margin: 0; font-size: 24px;"">TRUSTSIM</h1>
            </div>
            <div style=""padding: 30px 25px;"">
                <h2 style=""color: #1e293b; font-size: 20px; margin-top: 0;"">{title}</h2>
                <div style=""font-size: 15px; color: #475569; line-height: 1.6;"">{message}</div>
                {actionBtn}
            </div>
            <div style=""background-color: #f8fafc; padding: 15px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 12px; color: #94a3b8;"">
                <p style=""margin: 0;"">© 2026 TrustSim Platform.</p>
            </div>
        </div>";
    }
}
