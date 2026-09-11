using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ExportSimsQuery(
    string? Keyword = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    Guid? PrefixId = null,
    SimStatus? Status = null
) : IRequest<byte[]>;
