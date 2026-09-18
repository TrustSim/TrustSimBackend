namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối tới Google Gemini API (thông qua Semantic Kernel)
/// </summary>
public class GeminiSettings
{
    public const string SectionName = "GeminiSettings";

    /// <summary>Google Gemini API Key (bắt đầu bằng AIzaSy...)</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Endpoint OpenAI-compatible của Google Gemini (mặc định: https://generativelanguage.googleapis.com/v1beta/openai/)</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai/";

    /// <summary>Tên mô hình Chat chính (mặc định: gemini-1.5-flash)</summary>
    public string ChatModel { get; set; } = "gemini-1.5-flash";

    /// <summary>Tên mô hình Embedding chính (mặc định: text-embedding-004)</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-004";

    /// <summary>Số chiều của vector embedding (mặc định: 768)</summary>
    public int EmbeddingDimension { get; set; } = 768;

    /// <summary>Thời gian timeout tối đa cho mỗi request AI (giây)</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
