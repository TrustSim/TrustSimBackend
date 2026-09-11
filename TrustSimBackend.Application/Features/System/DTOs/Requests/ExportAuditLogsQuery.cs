using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Requests;

public record ExportAuditLogsQuery(
    string? EntityName = null,
    ActionType? ActionType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
) : IRequest<byte[]>;
