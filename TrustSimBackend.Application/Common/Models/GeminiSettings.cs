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

    /// <summary>Tên mô hình Chat chính (mặc định: gemini-3-flash-preview)</summary>
    public string ChatModel { get; set; } = "gemini-3-flash-preview";

    /// <summary>Tên mô hình Multimodal Vision / OCR chính (mặc định: gemini-3-flash-preview)</summary>
    public string VisionModel { get; set; } = "gemini-3-flash-preview";

    /// <summary>Tên mô hình Embedding chính (mặc định: text-embedding-004)</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-004";

    /// <summary>Số chiều của vector embedding (mặc định: 768)</summary>
    public int EmbeddingDimension { get; set; } = 768;

    /// <summary>Thời gian timeout tối đa cho mỗi request AI (giây)</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Per-candidate HTTP timeout in seconds for chat failover attempts.</summary>
    public int CandidateTimeoutSeconds { get; set; } = 16;

    /// <summary>Per-candidate HTTP timeout in seconds for heavy SIM valuation JSON generation.</summary>
    public int ValuationCandidateTimeoutSeconds { get; set; } = 24;

    /// <summary>Total wall-clock timeout budget in seconds for the multi-turn LLM loop before falling back locally.</summary>
    public int OverallAgentTimeoutSeconds { get; set; } = 32;

    /// <summary>Advanced model name selected by the router for complex valuation or multi-criteria queries.</summary>
    public string ComplexChatModel { get; set; } = "gemini-3-flash-preview";

    /// <summary>Primary model name used for AI SIM valuation reasoning.</summary>
    public string ValuationModel { get; set; } = "gemini-3-flash-preview";

    /// <summary>Ordered candidate models bound from appsettings.json for Fast tier routing.</summary>
    public List<string> FastModels { get; set; } = new();

    /// <summary>Ordered candidate models bound from appsettings.json for Reasoning tier routing.</summary>
    public List<string> ReasoningModels { get; set; } = new();

    /// <summary>Ordered candidate models bound from appsettings.json for SIM valuation failover.</summary>
    public List<string> ValuationModels { get; set; } = new();

    /// <summary>Ordered candidate fallback models bound from appsettings.json for Vision OCR failover.</summary>
    public List<string> VisionFallbackModels { get; set; } = new();

    /// <summary>Enables L1 memory + L2 Redis response caching for deterministic consultation queries.</summary>
    public bool EnableResponseCache { get; set; } = true;

    /// <summary>TTL in minutes for cached AI consultation responses.</summary>
    public int ResponseCacheTtlMinutes { get; set; } = 30;

    /// <summary>TTL in minutes for cached RAG query embedding vectors.</summary>
    public int EmbeddingCacheTtlMinutes { get; set; } = 120;

    /// <summary>Maximum AI consultation messages allowed per minute per user or client IP.</summary>
    public int RateLimitRequestsPerMinute { get; set; } = 20;

    /// <summary>Maximum input message length enforced by input guardrails.</summary>
    public int MaxMessageLength { get; set; } = 2000;

    /// <summary>Enables Semantic Response Cache (Cosine Similarity matching on paraphrases) when Exact Hash misses.</summary>
    public bool EnableSemanticCache { get; set; } = true;

    /// <summary>Minimum Cosine Similarity threshold [0..1] required for a Semantic Cache HIT (default: 0.90).</summary>
    public double SemanticCacheSimilarityThreshold { get; set; } = 0.90;

    /// <summary>Maximum number of semantic cache entries kept per scope bucket.</summary>
    public int SemanticCacheMaxEntriesPerScope { get; set; } = 50;
}
