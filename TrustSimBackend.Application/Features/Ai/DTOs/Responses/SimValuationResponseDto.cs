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
    public decimal RetailPrice => MaxPrice;    // Giá mua mới đại lý
    public decimal P2pPrice => SuggestedPrice; // Giá thị trường tự do P2P
    public decimal DealerPrice => MinPrice;    // Giá thợ thu mua thanh lý
    public int TotalPoints { get; set; } = 5;  // Tổng nút (1-10)
    public string PrefixGroup { get; set; } = string.Empty;
    public string MiddleGroup { get; set; } = string.Empty;
    public string TailGroup { get; set; } = string.Empty;
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
    public string EngineSource { get; set; } = "SEMANTIC_KERNEL_GEMINI";
    public long ExecutionTimeMs { get; set; }
    public bool AppliedToSim { get; set; }
    public DateTime CreatedDate { get; set; }
}
