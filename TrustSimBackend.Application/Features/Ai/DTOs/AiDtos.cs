using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs;

public class IngestDocumentRequest
{
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; } = DocumentType.MARKET_RESEARCH;
}

public class IngestDocumentResponseDto
{
    public Guid DocumentId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public DocumentStatus Status { get; set; }
    public int TotalChunks { get; set; }
    public int TotalTokens { get; set; }
    public DateTime? CrawledAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EvaluateSimPriceRequest
{
    /// <summary>ID của số SIM đã có trong hệ thống</summary>
    public Guid? SimNumberId { get; set; }

    /// <summary>Hoặc số điện thoại cần định giá trực tiếp (nếu chưa lưu trong DB)</summary>
    public string? PhoneNumber { get; set; }
}

public class SimValuationResponseDto
{
    public Guid ValuationId { get; set; }
    public Guid? SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal SuggestedPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public float ConfidenceScore { get; set; }
    public decimal BaselinePrice { get; set; }
    public float MarketMultiplier { get; set; }
    public string DetectedRhythmName { get; set; } = string.Empty;
    public string DetectedRhythmCode { get; set; } = string.Empty;
    public List<string> DetectedCategories { get; set; } = new();
    public string ValuationReasoning { get; set; } = string.Empty;
    public string? FengShuiAnalysis { get; set; }
    public List<string> ReferencedDocuments { get; set; } = new();
    public string AiModelName { get; set; } = string.Empty;
    public bool AppliedToSim { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ApplyValuationPriceRequest
{
    public Guid ValuationId { get; set; }
}

public class ChatConsultationRequest
{
    public Guid? SessionId { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? UserBirthYear { get; set; }
    public string? UserCareer { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public FengShuiElement? FengShuiElement { get; set; }
}

public class ChatConsultationResponseDto
{
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public string ReplyContent { get; set; } = string.Empty;
    public List<ConsultationSimDto> SuggestedSims { get; set; } = new();
    public List<string> ReferencedDocuments { get; set; } = new();
}

public class ConsultationSimDto
{
    public Guid SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string HighlightReason { get; set; } = string.Empty;
}

public class ConsultationSessionDto
{
    public Guid Id { get; set; }
    public string SessionTitle { get; set; } = string.Empty;
    public decimal? TargetBudgetMin { get; set; }
    public decimal? TargetBudgetMax { get; set; }
    public int? UserBirthYear { get; set; }
    public string? UserCareer { get; set; }
    public FengShuiElement? FengShuiElement { get; set; }
    public DateTime CreatedDate { get; set; }
    public int MessageCount { get; set; }
}

public class ConsultationMessageDto
{
    public Guid Id { get; set; }
    public ChatSenderRole SenderRole { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
