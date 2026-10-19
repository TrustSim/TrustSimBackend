using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record GetUnifiedWalletStatementQuery(
    Guid UserId,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<UnifiedWalletTransactionDto>>;
