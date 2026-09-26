using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; } = DocumentType.GENERAL;
    public DocumentStatus Status { get; set; } = DocumentStatus.PENDING;
    public int TotalChunks { get; set; }
    public int TotalTokens { get; set; }
    public DateTime? CrawledAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Audit Info
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime UpdatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedDate { get; set; }
    public string? DeletedBy { get; set; }
}
