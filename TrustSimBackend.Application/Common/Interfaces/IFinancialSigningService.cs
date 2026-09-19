namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ ký số mật mã học mô phỏng HSM (Hardware Security Module) phục vụ toàn vẹn tài chính.
/// Sử dụng thuật toán ECDsa (Curve NIST P-256 / secp256r1) để ký và xác thực mã băm SHA-256 của các mắt xích sổ cái.
/// </summary>
public interface IFinancialSigningService
{
    /// <summary>
    /// Ký số trên chuỗi mã băm SHA-256 bằng Master Private Key của sàn.
    /// </summary>
    /// <param name="hashHex">Chuỗi băm SHA-256 (64 ký tự hex).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Chữ ký số định dạng Base64 (IEEE P1363 format).</returns>
    Task<string> SignHashAsync(string hashHex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Xác thực tính hợp lệ của chữ ký số bằng Public Key của sàn.
    /// </summary>
    /// <param name="hashHex">Chuỗi băm SHA-256 cần kiểm tra.</param>
    /// <param name="signatureBase64">Chữ ký số Base64 cần xác thực.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True nếu chữ ký hợp lệ và chưa từng bị can thiệp.</returns>
    Task<bool> VerifySignatureAsync(string hashHex, string signatureBase64, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lấy Public Key ở định dạng chuẩn PEM để công khai cho Verifier độc lập.
    /// </summary>
    string GetPublicKeyPem();
}
