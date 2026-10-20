using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminGetAllTransactionsQuery(
    AdminWalletTransactionsFilter Filter
) : IRequest<PagedResult<WalletTransactionDto>>;
