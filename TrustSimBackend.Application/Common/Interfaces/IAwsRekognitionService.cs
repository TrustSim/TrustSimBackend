namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Hợp đồng dịch vụ trí tuệ nhân tạo thị giác máy tính AWS Rekognition phục vụ eKYC.
/// </summary>
public interface IAwsRekognitionService
{
    /// <summary>
    /// Trích xuất toàn bộ văn bản (OCR) từ hình ảnh giấy tờ tùy thân (CMND/CCCD/Hộ chiếu).
    /// </summary>
    Task<OcrExtractionResult> ExtractDocumentTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kiểm tra chất lượng khuôn mặt trên ảnh Selfie (đảm bảo đúng 1 khuôn mặt, không bị che khuất / kính râm).
    /// </summary>
    Task<FaceDetectionResult> ValidateSelfieFaceAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// So khớp khuôn mặt giữa ảnh chân dung thẻ CCCD (Source) và ảnh Selfie (Target).
    /// </summary>
    Task<FaceMatchResult> CompareFacesAsync(byte[] sourceImageBytes, byte[] targetImageBytes, float similarityThreshold = 70.0f, CancellationToken cancellationToken = default);
}

/// <summary>
/// DTO chứa kết quả trích xuất văn bản từ thẻ căn cước.
/// </summary>
public class OcrExtractionResult
{
    public bool IsSuccess { get; set; }
    public string? IdentityNumber { get; set; }
    public string? FullName { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Address { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public float OverallConfidence { get; set; }
    public string RawJson { get; set; } = string.Empty;
    public List<string> DetectedLines { get; set; } = new();
}

/// <summary>
/// DTO chứa kết quả kiểm định khuôn mặt selfie.
/// </summary>
public class FaceDetectionResult
{
    public bool IsValid { get; set; }
    public int FaceCount { get; set; }
    public float Brightness { get; set; }
    public float Sharpness { get; set; }
    public bool HasSunglasses { get; set; }
    public bool EyesOpen { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// DTO chứa kết quả so khớp khuôn mặt giữa CCCD và Selfie.
/// </summary>
public class FaceMatchResult
{
    public bool IsMatched { get; set; }
    public float SimilarityScore { get; set; }
    public float ConfidenceScore { get; set; }
    public string? ErrorMessage { get; set; }
}
