using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record GetSimImportTemplateQuery() : IRequest<byte[]>;
