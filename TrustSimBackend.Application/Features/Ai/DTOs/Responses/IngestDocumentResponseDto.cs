using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

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
