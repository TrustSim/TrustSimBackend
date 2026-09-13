namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả bóc tách đặc trưng số học & nhịp số của SIM (Tầng 1)
/// </summary>
public class SimExtractedFeatures
{
    public string RawPhoneNumber { get; set; } = string.Empty;
    public string CleanPhoneNumber { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public float PrefixReputationScore { get; set; } = 1.0f;
    public string DetectedRhythmCode { get; set; } = "SIM_THUONG";
    public string DetectedRhythmName { get; set; } = "SIM Thường";
    public float RhythmBaseFactor { get; set; } = 1.0f;
    public List<SimCategoryFeature> DetectedCategories { get; set; } = new();
    public bool HasAvoidNumbers { get; set; } = false; // Có chứa 49, 53 hoặc nhiều 4, 7 kỵ
    public bool HasLuckyEnd { get; set; } = false; // Đuôi 68, 86, 79, 39, 88, 99
    public string FengShuiNotes { get; set; } = string.Empty;
}

public class SimCategoryFeature
{
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public float Multiplier { get; set; } = 1.0f;
    public float ScoreContribution { get; set; } = 1.0f;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả tính giá cơ sở toán học (Tầng 2)
/// </summary>
public class SimMathValuationResult
{
    public decimal BaselinePrice { get; set; }
    public decimal BasePriceStandard { get; set; } = 200000m;
    public float PrefixFactor { get; set; } = 1.0f;
    public float RhythmFactor { get; set; } = 1.0f;
    public float CategoryFactor { get; set; } = 1.0f;
    public float FengShuiFactor { get; set; } = 1.0f;
    public string FormulaExplanation { get; set; } = string.Empty;
}

/// <summary>
/// Phản hồi cấu trúc JSON từ NVIDIA LLM (Tầng 4)
/// </summary>
public class AiValuationLlmResponse
{
    public decimal SuggestedPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public float ConfidenceScore { get; set; } = 0.9f;
    public float MarketMultiplier { get; set; } = 1.0f;
    public string RhythmRecommendation { get; set; } = string.Empty;
    public List<string> CategoryCodes { get; set; } = new();
    public string ValuationReasoning { get; set; } = string.Empty;
    public string FengShuiSummary { get; set; } = string.Empty;
}

/// <summary>
/// Đoạn văn bản sau khi chia nhỏ (Chunking)
/// </summary>
public class TextChunkItem
{
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public int TokenCount { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Kết quả tìm kiếm Vector Cosine Similarity (RAG)
/// </summary>
public class DocumentChunkSearchResult
{
    public Guid ChunkId { get; set; }
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public string? MetadataJson { get; set; }
}

/// <summary>
/// Đề xuất số SIM thực tế trong kho kèm lý do gợi ý
/// </summary>
public class ConsultationSimSuggestion
{
    public Guid SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string HighlightReason { get; set; } = string.Empty;
}

/// <summary>
/// Phản hồi tư vấn từ AI Chatbot
/// </summary>
public class AiConsultationLlmResponse
{
    public string ReplyContent { get; set; } = string.Empty;
    public List<ConsultationSimSuggestion> SuggestedSims { get; set; } = new();
    public List<string> ReferencedDocuments { get; set; } = new();
    public int TokensUsed { get; set; }
}
