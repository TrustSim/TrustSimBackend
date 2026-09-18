using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public class IngestDocumentRequest
{
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; } = DocumentType.MARKET_RESEARCH;
}
