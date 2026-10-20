using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Xử lý truy vấn danh sách yêu cầu rút tiền cho Admin.
/// </summary>
public class AdminGetWithdrawalsQueryHandler : IRequestHandler<AdminGetWithdrawalsQuery, PagedResult<WalletTransactionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminGetWithdrawalsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<WalletTransactionDto>> Handle(AdminGetWithdrawalsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.WalletTransactions.GetPagedWithdrawalsAsync(
            request.Filter,
            cancellationToken);

        var dtos = items.Select(t => new WalletTransactionDto
        {
            Id = t.Id,
            WalletId = t.WalletId,
            TransactionCode = t.TransactionCode,
            Amount = t.Amount,
            TransactionStatus = t.TransactionStatus,
            WalletTransactionType = t.WalletTransactionType,
            BalanceBefore = t.BalanceBefore,
            BalanceAfter = t.BalanceAfter,
            DestinationBankCode = t.DestinationBankCode,
            DestinationBankName = t.DestinationBankName,
            DestinationAccountNumberMasked = t.DestinationAccountNumberMasked,
            DestinationAccountHolderName = t.DestinationAccountHolderName,
            Description = t.Description,
            CreatedAt = t.CreatedAt,
            CompletedAt = t.CompletedAt,
            ExpiredAt = t.ExpiredAt
        }).ToList();

        return PagedResult<WalletTransactionDto>.Create(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
