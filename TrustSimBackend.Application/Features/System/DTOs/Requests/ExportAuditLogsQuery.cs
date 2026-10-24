using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record ExportAuditLogsQuery(AuditLogQueryFilter? Filter = null) : IRequest<byte[]>;
