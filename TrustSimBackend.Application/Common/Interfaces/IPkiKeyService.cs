using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ sinh và quản lý cặp khóa ký số điện tử PKI (RSA / AES).
/// </summary>
public interface IPkiKeyService
{
    /// <summary>
    /// Sinh cặp khóa RSA 2048-bit mới, xuất Public Key dạng PEM và mã hóa Private Key bằng AES-256.
    /// </summary>
    /// <param name="encryptionPassphrase">Khóa bí mật dùng để mã hóa Private Key (tùy chọn, mặc định sử dụng Master Key hệ thống).</param>
    /// <returns>Đối tượng chứa PublicKey PEM, EncryptedPrivateKey Base64 và KeyHash SHA-256.</returns>
    PkiKeyPairResult GenerateKeyPair(string? encryptionPassphrase = null);
}
