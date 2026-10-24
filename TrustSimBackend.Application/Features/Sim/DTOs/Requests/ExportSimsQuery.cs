using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ExportSimsQuery(AdminSimsQueryFilter? Filter = null) : IRequest<byte[]>;
