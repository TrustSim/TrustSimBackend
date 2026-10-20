using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record GetUnifiedWalletStatementQuery(
    Guid UserId,
    UnifiedStatementQueryFilter Filter
) : IRequest<PagedResult<UnifiedWalletTransactionDto>>;
