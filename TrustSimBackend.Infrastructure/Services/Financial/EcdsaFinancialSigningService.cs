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

    private const string KeyFileName = "financial_hsm_key.pem";

    public EcdsaFinancialSigningService(ILogger<EcdsaFinancialSigningService> logger)
    {
        _logger = logger;
        _ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var keysDir = Path.Combine(AppContext.BaseDirectory, "Keys");
        Directory.CreateDirectory(keysDir);
        var keyFilePath = Path.Combine(keysDir, KeyFileName);

        try
        {
            if (File.Exists(keyFilePath))
            {
                var pemContent = File.ReadAllText(keyFilePath);
                _ecdsa.ImportFromPem(pemContent);
                _logger.LogInformation("[Financial HSM] Đã nạp thành công Master Financial Signing Key từ file '{KeyPath}'.", keyFilePath);
            }
            else
            {
                var privateKeyPem = _ecdsa.ExportECPrivateKeyPem();
                File.WriteAllText(keyFilePath, privateKeyPem);
                _logger.LogInformation("[Financial HSM] Đã sinh và lưu trữ mới Master Financial Signing Key tại '{KeyPath}'.", keyFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Financial HSM] Không thể đọc/ghi file khóa '{KeyPath}'. Sử dụng khóa tạm thời trong bộ nhớ: {Message}", keyFilePath, ex.Message);
        }

        _publicKeyPem = _ecdsa.ExportSubjectPublicKeyInfoPem();
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
