using MediatR;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record ExportDocumentsQuery(bool? ActiveOnly = null) : IRequest<byte[]>;
