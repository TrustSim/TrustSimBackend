using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ExportPhonePrefixesQuery(Guid? CarrierId = null) : IRequest<byte[]>;
