namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi ảnh selfie không đạt tiêu chuẩn (không có mặt, đeo kính râm, hoặc nhiều người).
/// </summary>
public class KycImageQualityException : Exception
{
    public KycImageQualityException(string message) : base(message) { }
}
