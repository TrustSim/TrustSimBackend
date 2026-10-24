using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record ExportDocumentsQuery(DocumentQueryFilter? Filter = null) : IRequest<byte[]>;
