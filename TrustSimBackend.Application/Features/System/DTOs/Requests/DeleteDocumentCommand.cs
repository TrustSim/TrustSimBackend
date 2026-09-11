using MediatR;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record DeleteDocumentCommand(Guid Id) : IRequest<bool>;
