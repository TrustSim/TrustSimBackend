using Microsoft.AspNetCore.Http;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public class UploadDocumentRequest
{
    public IFormFile File { get; set; } = null!;
    public string? DocumentName { get; set; }
    public DocumentType DocumentType { get; set; } = DocumentType.MARKET_RESEARCH;
}
