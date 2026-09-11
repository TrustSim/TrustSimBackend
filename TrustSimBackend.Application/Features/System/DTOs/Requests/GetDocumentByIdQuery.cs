using MediatR;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record GetDocumentByIdQuery(Guid Id) : IRequest<DocumentDto>;
