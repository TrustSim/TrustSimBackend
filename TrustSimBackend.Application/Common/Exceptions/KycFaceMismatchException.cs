namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi tỷ lệ so khớp khuôn mặt giữa CCCD và Selfie dưới ngưỡng an toàn.
/// </summary>
public class KycFaceMismatchException : Exception
{
    public float SimilarityScore { get; }

    public KycFaceMismatchException(float similarityScore)
        : base($"Khuôn mặt trong ảnh chân dung selfie không trùng khớp với ảnh trên giấy tờ tùy thân (Độ tương đồng: {similarityScore:F1}%). Vui lòng thử lại với góc chụp rõ nét hơn.")
    {
        SimilarityScore = similarityScore;
    }
}
