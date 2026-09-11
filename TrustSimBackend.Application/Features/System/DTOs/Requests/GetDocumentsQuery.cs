using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetDocumentsQuery(bool ActiveOnly = true) : IRequest<IReadOnlyList<DocumentDto>>;
