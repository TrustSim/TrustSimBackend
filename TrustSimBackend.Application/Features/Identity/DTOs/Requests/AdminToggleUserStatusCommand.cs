using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminToggleUserStatusCommand(Guid Id, bool IsActive) : IRequest<bool>;
