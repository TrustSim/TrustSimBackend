using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

public class AdminGetAllTransactionsQueryHandler : IRequestHandler<AdminGetAllTransactionsQuery, PagedResult<WalletTransactionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminGetAllTransactionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<WalletTransactionDto>> Handle(AdminGetAllTransactionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.WalletTransactions.GetPagedAllAsync(
            request.Filter,
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

        return new PagedResult<WalletTransactionDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
