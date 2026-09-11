using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Xử lý truy vấn phân trang danh sách lịch sử nạp / rút tiền ví cá nhân.
/// </summary>
public class GetWalletTransactionsQueryHandler : IRequestHandler<GetWalletTransactionsQuery, PagedResult<WalletTransactionDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;

    public GetWalletTransactionsQueryHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository walletTransactionRepository)
    {
        _walletRepository = walletRepository;
        _walletTransactionRepository = walletTransactionRepository;
    }

    public async Task<PagedResult<WalletTransactionDto>> Handle(GetWalletTransactionsQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        var (items, totalCount) = await _walletTransactionRepository.GetPagedByWalletIdAsync(
            wallet.Id,
            request.Type,
            request.Status,
            request.PageIndex,
            request.PageSize,
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

        return PagedResult<WalletTransactionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
