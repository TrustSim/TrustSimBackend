namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình kết nối tới dịch vụ NVIDIA NIM API (build.nvidia.com)
/// </summary>
public class NvidiaSettings
{
    public const string SectionName = "NvidiaSettings";

    /// <summary>Endpoint gốc của NVIDIA NIM API (mặc định: https://integrate.api.nvidia.com/v1)</summary>
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";

    /// <summary>NVIDIA API Key bắt đầu bằng nvapi-...</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Tên mô hình LLM chính (mặc định: meta/llama-3.3-70b-instruct)</summary>
    public string LlmModel { get; set; } = "meta/llama-3.3-70b-instruct";

    /// <summary>Tên mô hình Embedding chính (mặc định: nvidia/nv-embedqa-e5-v5)</summary>
    public string EmbeddingModel { get; set; } = "nvidia/nv-embedqa-e5-v5";

    /// <summary>Số chiều của vector embedding (mặc định: 1024)</summary>
    public int EmbeddingDimension { get; set; } = 1024;

    /// <summary>Thời gian timeout tối đa cho mỗi request AI (giây)</summary>
    public int TimeoutSeconds { get; set; } = 60;
}
