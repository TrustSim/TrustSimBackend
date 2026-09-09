namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối và các ngưỡng độ tin cậy của AWS Rekognition.
/// </summary>
public class AwsRekognitionSettings
{
    public const string SectionName = "AwsRekognitionSettings";

    /// <summary>AWS Access Key ID.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>AWS Secret Access Key.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Vùng máy chủ AWS (mặc định: ap-southeast-1 - Singapore).</summary>
    public string Region { get; set; } = "ap-southeast-1";

    /// <summary>Ngưỡng độ tin cậy tối thiểu khi nhận dạng văn bản (0 - 100).</summary>
    public float MinConfidenceThreshold { get; set; } = 80.0f;

    /// <summary>Ngưỡng tỷ lệ so khớp khuôn mặt để TỰ ĐỘNG DUYỆT (mặc định: 85.0%).</summary>
    public float AutoApproveSimilarityThreshold { get; set; } = 85.0f;

    /// <summary>Ngưỡng tỷ lệ so khớp tối thiểu để CHUYỂN ADMIN DUYỆT (mặc định: 70.0%). Dưới ngưỡng này sẽ tự động từ chối.</summary>
    public float ManualReviewSimilarityThreshold { get; set; } = 70.0f;
}
