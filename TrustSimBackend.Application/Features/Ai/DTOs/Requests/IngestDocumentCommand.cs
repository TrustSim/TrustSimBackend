using MediatR;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Requests;

public record IngestDocumentCommand(
    string DocumentName,
    string LinkUrl,
    DocumentType DocumentType = DocumentType.MARKET_RESEARCH
) : IRequest<IngestDocumentResponseDto>;
