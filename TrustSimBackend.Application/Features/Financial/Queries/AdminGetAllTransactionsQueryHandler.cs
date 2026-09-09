using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

public class AdminGetAllTransactionsQueryHandler : IRequestHandler<AdminGetAllTransactionsQuery, PagedResult<WalletTransactionDto>>
{
    private readonly IWalletTransactionRepository _transactionRepository;

    public AdminGetAllTransactionsQueryHandler(IWalletTransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    public async Task<PagedResult<WalletTransactionDto>> Handle(AdminGetAllTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _transactionRepository.GetPagedAllAsync(
            request.Type,
            request.Status,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(t => new WalletTransactionDto
        {
            Id = t.Id,
            TransactionCode = t.TransactionCode,
            WalletTransactionType = t.WalletTransactionType,
            Amount = t.Amount,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            TransactionStatus = t.TransactionStatus,
            Description = t.Description,
            CreatedAt = t.CreatedDate
        }).ToList();

        return new PagedResult<WalletTransactionDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
