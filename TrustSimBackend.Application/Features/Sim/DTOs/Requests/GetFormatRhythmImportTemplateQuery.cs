using MediatR;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record GetFormatRhythmImportTemplateQuery() : IRequest<byte[]>;
