using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record UpdateDocumentCommand(
    Guid Id = default,
    string DocumentName = "",
    string LinkUrl = ""
) : IRequest<DocumentDto>;
