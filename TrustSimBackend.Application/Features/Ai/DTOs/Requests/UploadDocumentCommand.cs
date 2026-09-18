using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record UploadDocumentCommand(
    string DocumentName,
    IFormFile File,
    DocumentType DocumentType = DocumentType.MARKET_RESEARCH
) : IRequest<IngestDocumentResponseDto>;
