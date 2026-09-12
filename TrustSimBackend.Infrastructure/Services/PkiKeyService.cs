using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ mật mã học tạo cặp khóa RSA 2048-bit và mã hóa khóa bí mật AES-256.
/// </summary>
public class PkiKeyService : IPkiKeyService
{
    private readonly ILogger<PkiKeyService> _logger;
    private const string DefaultMasterSecret = "TrustSim_PKI_Master_Salt_Key_2026_Secured!";

    public PkiKeyService(ILogger<PkiKeyService> logger)
    {
        _logger = logger;
    }

    public PkiKeyPairResult GenerateKeyPair(string? encryptionPassphrase = null)
    {
        using var rsa = RSA.Create(2048);

        // 1. Xuất Public Key chuẩn PEM (SubjectPublicKeyInfo)
        var publicKeyPem = rsa.ExportSubjectPublicKeyInfoPem();

        // 2. Xuất Private Key dạng PKCS#8
        var privateKeyBytes = rsa.ExportPkcs8PrivateKey();
        var privateKeyBase64 = Convert.ToBase64String(privateKeyBytes);

        // 3. Mã hóa Private Key bằng AES-256
        var secret = string.IsNullOrWhiteSpace(encryptionPassphrase) ? DefaultMasterSecret : encryptionPassphrase;
        var encryptedPrivateKey = EncryptAes256(privateKeyBase64, secret);

        // 4. Tính toán KeyHash (SHA-256 của chuỗi Public Key PEM)
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(publicKeyPem));
        var keyHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

        _logger.LogInformation("[PKI Service] Đã khởi tạo thành công cặp khóa RSA-2048 mới (KeyHash: {KeyHash})", keyHash);

        return new PkiKeyPairResult
        {
            PublicKeyPem = publicKeyPem,
            EncryptedPrivateKey = encryptedPrivateKey,
            KeyHash = keyHash
        };
    }

    private static string EncryptAes256(string plainText, string secret)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;

        using var sha256 = SHA256.Create();
        aes.Key = sha256.ComputeHash(Encoding.UTF8.GetBytes(secret));
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        // Ghép IV + CipherBytes và encode Base64
        var combined = new byte[aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, combined, 0, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, combined, aes.IV.Length, cipherBytes.Length);

        return Convert.ToBase64String(combined);
    }
}
