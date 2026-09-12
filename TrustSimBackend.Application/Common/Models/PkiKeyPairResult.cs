namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả sinh cặp khóa mật mã học PKI (RSA-2048 & AES-256).
/// </summary>
public class PkiKeyPairResult
{
    /// <summary>
    /// Khóa công khai định dạng PEM chuẩn SubjectPublicKeyInfo.
    /// </summary>
    public string PublicKeyPem { get; set; } = string.Empty;

    /// <summary>
    /// Khóa bí mật định dạng PKCS#8 đã được mã hóa an toàn bằng AES-256 (Base64).
    /// </summary>
    public string EncryptedPrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Mã băm SHA-256 của Public Key dùng để kiểm tra tính toàn vẹn.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;
}
