using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Requests;

public record AdminExportDisputesQuery(DisputeQueryFilter? Filter = null) : IRequest<byte[]>;
