namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ nhận diện ký tự quang học (OCR) thông qua Google Gemini Multimodal Vision API.
/// </summary>
public interface IGoogleVisionService
{
    /// <summary>
    /// Nhận diện toàn bộ chữ thô từ mảng byte ảnh.
    /// </summary>
    Task<GoogleVisionOcrResult> DetectAllTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Nhận diện toàn bộ chữ thô từ Stream ảnh.
    /// </summary>
    Task<GoogleVisionOcrResult> DetectAllTextFromStreamAsync(Stream imageStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Kết quả trích xuất OCR từ Google Cloud Vision.
/// </summary>
public class GoogleVisionOcrResult
{
    public bool IsSuccess { get; set; }
    public string FullText { get; set; } = string.Empty;
    public List<GoogleVisionOcrLine> Lines { get; set; } = new();
    public string RawJson { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Đại diện cho một dòng chữ trích xuất được kèm độ tin cậy.
/// </summary>
public class GoogleVisionOcrLine
{
    public int LineIndex { get; set; }
    public string Text { get; set; } = string.Empty;
    public float Confidence { get; set; }
}
