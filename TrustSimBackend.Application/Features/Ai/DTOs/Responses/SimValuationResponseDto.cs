namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class SimValuationResponseDto
{
    public Guid ValuationId { get; set; }
    public Guid? SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string FormattedNumber { get; set; } = string.Empty;
    public decimal SuggestedPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public float ConfidenceScore { get; set; }
    public decimal BaselinePrice { get; set; }
    public float MarketMultiplier { get; set; }
    public string SelectedRhythmValue { get; set; } = string.Empty;
    public string DetectedRhythmName { get; set; } = string.Empty;
    public string DetectedRhythmCode { get; set; } = string.Empty;
    public List<string> DetectedCategories { get; set; } = new();
    public string ValuationReasoning { get; set; } = string.Empty;
    public string? FengShuiAnalysis { get; set; }
    public string? AlternativeRhythmsComparison { get; set; }
    public List<string> ReferencedDocuments { get; set; } = new();
    public string AiModelName { get; set; } = string.Empty;
    public string EngineSource { get; set; } = "NVIDIA_CLOUD_LLM"; // "NVIDIA_CLOUD_LLM" | "LOCAL_FALLBACK_ENGINE"
    public long ExecutionTimeMs { get; set; }
    public bool AppliedToSim { get; set; }
    public DateTime CreatedDate { get; set; }
}
