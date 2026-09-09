using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record AdminExportDisputesQuery(DisputeStatus? Status = null) : IRequest<byte[]>;
