using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Giao diện kết nối HTTP Client tới dịch vụ phần cứng bảo mật ảo Universal SoftHSMv2 Microservice.
/// </summary>
public interface ISoftHsmClient
{
    /// <summary>Kiểm tra trạng thái sẵn sàng của dịch vụ SoftHSM.</summary>
    Task<HsmHealthResponse> CheckHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Sinh cặp khóa mật mã mới (RSA hoặc ECDSA).</summary>
    Task<HsmGenerateKeyResponse> GenerateKeyPairAsync(HsmGenerateKeyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Ký số mật mã học trên văn bản hoặc mã băm.</summary>
    Task<HsmSignResponse> SignAsync(HsmSignRequest request, CancellationToken cancellationToken = default);

    /// <summary>Xác thực chữ ký số từ xa qua SoftHSM API.</summary>
    Task<HsmVerifyResponse> VerifyAsync(HsmVerifyRequest request, CancellationToken cancellationToken = default);

    /// <summary>Mã hóa dữ liệu bảo vệ (Envelope Encryption / Sealed-Bid).</summary>
    Task<HsmEncryptResponse> EncryptAsync(HsmEncryptRequest request, CancellationToken cancellationToken = default);

    /// <summary>Giải mã dữ liệu bằng Private Key bảo vệ trong HSM.</summary>
    Task<HsmDecryptResponse> DecryptAsync(HsmDecryptRequest request, CancellationToken cancellationToken = default);

    /// <summary>Lấy Public Key định dạng chuẩn PEM (có hỗ trợ cache).</summary>
    Task<string> GetPublicKeyPemAsync(string keyLabel, CancellationToken cancellationToken = default);

    /// <summary>Kích hoạt sao lưu Token lên Cloud Storage (Supabase).</summary>
    Task<HsmBackupResponse> BackupAsync(CancellationToken cancellationToken = default);

    /// <summary>Kích hoạt phục hồi Token từ Cloud Storage.</summary>
    Task<HsmBackupResponse> RestoreAsync(CancellationToken cancellationToken = default);
}
