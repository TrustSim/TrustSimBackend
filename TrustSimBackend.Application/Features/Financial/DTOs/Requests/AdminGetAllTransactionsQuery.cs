using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminGetAllTransactionsQuery(
    Guid? UserId = null,
    WalletTransactionType? Type = null,
    TransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<WalletTransactionDto>>;
