using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Hsm;

/// <summary>
/// Triển khai dịch vụ ký số tài chính kết nối phần cứng bảo mật ảo Universal SoftHSMv2 Microservice.
/// Hỗ trợ chiến lược Xác thực Lai (Hybrid Verification) siêu tốc trong RAM bằng Public Key cache,
/// cùng cơ chế Auto-Provisioning tự động sinh Master Key nếu chưa tồn tại trên HSM.
/// </summary>
public class SoftHsmFinancialSigningService : IFinancialSigningService
{
    private readonly ISoftHsmClient _hsmClient;
    private readonly SoftHsmSettings _settings;
    private readonly ILogger<SoftHsmFinancialSigningService> _logger;

    private string? _cachedPublicKeyPem;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _isInitialized;

    public SoftHsmFinancialSigningService(
        ISoftHsmClient hsmClient,
        IOptions<SoftHsmSettings> settings,
        ILogger<SoftHsmFinancialSigningService> logger)
    {
        _hsmClient = hsmClient;
        _settings = settings.Value;
        _logger = logger;
    }

    private async Task EnsureMasterKeyInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized && !string.IsNullOrWhiteSpace(_cachedPublicKeyPem))
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized && !string.IsNullOrWhiteSpace(_cachedPublicKeyPem))
            {
                return;
            }

            try
            {
                _cachedPublicKeyPem = await _hsmClient.GetPublicKeyPemAsync(_settings.FinancialKeyLabel, cancellationToken);
                _isInitialized = true;
                _logger.LogInformation("[SoftHSM Financial] Đã tải thành công Master Financial Public Key cho nhãn '{KeyLabel}'.",
                    _settings.FinancialKeyLabel);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("[SoftHSM Financial] Chưa tìm thấy Master Financial Key '{KeyLabel}'. Đang tự động khởi tạo cặp khóa ECDSA mới trên HSM...",
                    _settings.FinancialKeyLabel);

                var genReq = new HsmGenerateKeyRequest(
                    KeyLabel: _settings.FinancialKeyLabel,
                    KeyType: "ECDSA",
                    Curve: "secp256r1");

                var genRes = await _hsmClient.GenerateKeyPairAsync(genReq, cancellationToken);
                _cachedPublicKeyPem = genRes.PublicKeyPem;
                _isInitialized = true;

                _logger.LogInformation("[SoftHSM Financial] Đã tự động sinh mới thành công Master Financial Key '{KeyLabel}'.",
                    _settings.FinancialKeyLabel);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> SignHashAsync(string hashHex, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashHex))
        {
            throw new ArgumentException("Mã băm không được để trống.", nameof(hashHex));
        }

        await EnsureMasterKeyInitializedAsync(cancellationToken);

        try
        {
            var signReq = new HsmSignRequest(
                KeyLabel: _settings.FinancialKeyLabel,
                DataHex: hashHex.Trim(),
                IsPrehashed: true,
                Mechanism: "AUTO");

            var res = await _hsmClient.SignAsync(signReq, cancellationToken);
            return res.SignatureBase64;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Trường hợp container SoftHSM vừa bị khởi động lại và mất token: Tự động hồi phục
            _logger.LogWarning("[SoftHSM Financial] Khóa bị mất do container reset. Tái sinh cặp khóa và ký lại...");
            _isInitialized = false;
            await EnsureMasterKeyInitializedAsync(cancellationToken);

            var retryReq = new HsmSignRequest(
                KeyLabel: _settings.FinancialKeyLabel,
                DataHex: hashHex.Trim(),
                IsPrehashed: true,
                Mechanism: "AUTO");

            var retryRes = await _hsmClient.SignAsync(retryReq, cancellationToken);
            return retryRes.SignatureBase64;
        }
    }

    public async Task<bool> VerifySignatureAsync(string hashHex, string signatureBase64, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hashHex) || string.IsNullOrWhiteSpace(signatureBase64))
        {
            return false;
        }

        await EnsureMasterKeyInitializedAsync(cancellationToken);

        // 1. Áp dụng Chiến lược Xác thực Lai (Hybrid Verification) trong RAM
        if (_settings.UseHybridVerification && !string.IsNullOrWhiteSpace(_cachedPublicKeyPem))
        {
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

                using var ecdsa = ECDsa.Create();
                ecdsa.ImportFromPem(_cachedPublicKeyPem);

                // Thử định dạng IEEE P1363 Fixed Field Concatenation (mặc định của PKCS#11 C_Sign)
                if (ecdsa.VerifyHash(hashBytes, signatureBytes, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                {
                    return true;
                }

                // Fallback thử định dạng ASN.1 RFC 3279 DER Sequence
                if (ecdsa.VerifyHash(hashBytes, signatureBytes, DSASignatureFormat.Rfc3279DerSequence))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SoftHSM Financial] Lỗi khi xác thực lai chữ ký trong RAM: {Message}", ex.Message);
            }
        }

        // 2. Fallback: Gửi request xác thực sang Remote SoftHSM API
        try
        {
            var verifyReq = new HsmVerifyRequest(
                KeyLabel: _settings.FinancialKeyLabel,
                DataHex: hashHex.Trim(),
                SignatureBase64: signatureBase64.Trim(),
                IsPrehashed: true,
                Mechanism: "AUTO");

            var verifyRes = await _hsmClient.VerifyAsync(verifyReq, cancellationToken);
            return verifyRes.Valid;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[SoftHSM Financial] Lỗi khi gửi lệnh đối soát sang SoftHSM Cloud: {Message}", ex.Message);
            return false;
        }
    }

    public string GetPublicKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_cachedPublicKeyPem))
        {
            return _cachedPublicKeyPem;
        }

        EnsureMasterKeyInitializedAsync().GetAwaiter().GetResult();
        return _cachedPublicKeyPem ?? string.Empty;
    }
}
