using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Triển khai dịch vụ ký số tài chính mô phỏng HSM sử dụng thuật toán ECDsa NIST P-256.
/// </summary>
public class EcdsaFinancialSigningService : IFinancialSigningService, IDisposable
{
    private readonly ECDsa _ecdsa;
    private readonly string _publicKeyPem;
    private readonly ILogger<EcdsaFinancialSigningService> _logger;

    public EcdsaFinancialSigningService(ILogger<EcdsaFinancialSigningService> logger)
    {
        _logger = logger;

        // Khởi tạo cặp khóa ECDsa NIST P-256 (secp256r1)
        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _publicKeyPem = _ecdsa.ExportSubjectPublicKeyInfoPem();

        _logger.LogInformation("[Financial HSM] Đã khởi tạo thành công Master Financial Signing Authority (ECDsa P-256).");
    }

    public Task<string> SignHashAsync(string hashHex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashHex))
        {
            throw new ArgumentException("Mã băm không được để trống.", nameof(hashHex));
        }

        byte[] hashBytes;
        try
        {
            hashBytes = Convert.FromHexString(hashHex.Trim());
        }
        catch
        {
            // Fallback: Nếu không phải chuẩn Hex 32-bytes, băm SHA-256 chuỗi text
            using var sha256 = SHA256.Create();
            hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashHex));
        }

        var signatureBytes = _ecdsa.SignHash(hashBytes);
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        return Task.FromResult(signatureBase64);
    }

    public Task<bool> VerifySignatureAsync(string hashHex, string signatureBase64, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashHex) || string.IsNullOrWhiteSpace(signatureBase64))
        {
            return Task.FromResult(false);
        }

        try
        {
            byte[] hashBytes;
            try
            {
                hashBytes = Convert.FromHexString(hashHex.Trim());
            }
            catch
            {
                using var sha256 = SHA256.Create();
                hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(hashHex));
            }

            var signatureBytes = Convert.FromBase64String(signatureBase64.Trim());
            var isValid = _ecdsa.VerifyHash(hashBytes, signatureBytes);

            return Task.FromResult(isValid);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Financial HSM] Lỗi khi giải mã và xác thực chữ ký số: {Message}", ex.Message);
            return Task.FromResult(false);
        }
    }

    public string GetPublicKeyPem()
    {
        return _publicKeyPem;
    }

    public void Dispose()
    {
        _ecdsa.Dispose();
    }
}
