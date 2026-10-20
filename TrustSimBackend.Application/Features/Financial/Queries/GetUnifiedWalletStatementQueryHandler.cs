using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

public class GetUnifiedWalletStatementQueryHandler : IRequestHandler<GetUnifiedWalletStatementQuery, PagedResult<UnifiedWalletTransactionDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetUnifiedWalletStatementQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<UnifiedWalletTransactionDto>> Handle(GetUnifiedWalletStatementQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException("Không tìm thấy ví tiền của người dùng.");
        }

        var filter = request.Filter;
        var unifiedList = new List<UnifiedWalletTransactionDto>();

        var loadWallet = string.IsNullOrWhiteSpace(filter.SourceType) ||
                         string.Equals(filter.SourceType, "WALLET", StringComparison.OrdinalIgnoreCase);

        var loadAuction = string.IsNullOrWhiteSpace(filter.SourceType) ||
                          !string.Equals(filter.SourceType, "WALLET", StringComparison.OrdinalIgnoreCase);

        if (loadWallet)
        {
            var walletTxs = await _unitOfWork.WalletTransactions.GetListByWalletIdAsync(wallet.Id, filter.FromDate, filter.ToDate, cancellationToken);
            foreach (var w in walletTxs)
            {
                var direction = w.WalletTransactionType switch
                {
                    WalletTransactionType.TOP_UP => "CREDIT",
                    WalletTransactionType.WITHDRAW => "DEBIT",
                    _ => "DEBIT"
                };

                unifiedList.Add(new UnifiedWalletTransactionDto
                {
                    Id = w.Id,
                    Source = "WALLET",
                    TransactionType = w.WalletTransactionType.ToString(),
                    Direction = direction,
                    Amount = w.Amount,
                    BalanceBefore = w.BalanceBefore,
                    BalanceAfter = w.BalanceAfter,
                    Status = w.TransactionStatus.ToString(),
                    ReferenceCode = w.TransactionCode,
                    Description = w.Description ?? GetWalletTypeDescription(w.WalletTransactionType),
                    CreatedAt = w.CreatedAt
                });
            }
        }

        if (loadAuction)
        {
            var auctionTxs = await _unitOfWork.AuctionTransactions.GetByWalletIdAsync(wallet.Id, filter.FromDate, filter.ToDate, cancellationToken);
            foreach (var a in auctionTxs)
            {
                var direction = a.AuctionTransactionType switch
                {
                    AuctionTransactionType.AUCTION_DEPOSIT_LOCK => "LOCK",
                    AuctionTransactionType.AUCTION_PROGRESSIVE_DEPOSIT_LOCK => "LOCK",
                    AuctionTransactionType.ESCROW_HOLD => "LOCK",
                    AuctionTransactionType.AUCTION_DEPOSIT_RELEASED => "CREDIT",
                    AuctionTransactionType.AUCTION_COMPENSATION => "CREDIT",
                    AuctionTransactionType.ESCROW_REFUNDED => "CREDIT",
                    AuctionTransactionType.SELLER_PAYOUT => "CREDIT",
                    AuctionTransactionType.TOP_UP_CREDIT => "CREDIT",
                    AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED => "DEBIT",
                    AuctionTransactionType.ESCROW_DEBIT => "DEBIT",
                    AuctionTransactionType.PENALTY => "DEBIT",
                    _ => "DEBIT"
                };

                unifiedList.Add(new UnifiedWalletTransactionDto
                {
                    Id = a.Id,
                    Source = "AUCTION",
                    TransactionType = a.AuctionTransactionType.ToString(),
                    Direction = direction,
                    Amount = a.Amount,
                    BalanceBefore = a.BalanceBefore,
                    BalanceAfter = a.BalanceAfter,
                    Status = a.TransactionStatus.ToString(),
                    ReferenceCode = a.JournalCode,
                    Description = GetAuctionTypeDescription(a.AuctionTransactionType),
                    CreatedAt = a.CreatedAt
                });
            }
        }

        var query = unifiedList.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim();
            query = query.Where(x =>
                (x.ReferenceCode != null && x.ReferenceCode.Contains(clean, StringComparison.OrdinalIgnoreCase)) ||
                (x.Description != null && x.Description.Contains(clean, StringComparison.OrdinalIgnoreCase)));
        }

        if (filter.MinAmount.HasValue)
        {
            query = query.Where(x => x.Amount >= filter.MinAmount.Value);
        }

        if (filter.MaxAmount.HasValue)
        {
            query = query.Where(x => x.Amount <= filter.MaxAmount.Value);
        }

        if (filter.SortBy == WalletTransactionSortBy.Amount)
        {
            query = filter.SortOrder == SortOrder.Asc
                ? query.OrderBy(x => x.Amount)
                : query.OrderByDescending(x => x.Amount);
        }
        else
        {
            query = filter.SortOrder == SortOrder.Asc
                ? query.OrderBy(x => x.CreatedAt)
                : query.OrderByDescending(x => x.CreatedAt);
        }

        var totalCount = query.Count();
        var pagedItems = query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<UnifiedWalletTransactionDto>(pagedItems, totalCount, filter.PageIndex, filter.PageSize);
    }

    private static string GetWalletTypeDescription(WalletTransactionType type) => type switch
    {
        WalletTransactionType.TOP_UP => "Nạp tiền vào ví qua cổng thanh toán",
        WalletTransactionType.WITHDRAW => "Rút tiền từ ví về tài khoản ngân hàng",
        _ => "Giao dịch ví"
    };

    private static string GetAuctionTypeDescription(AuctionTransactionType type) => type switch
    {
        AuctionTransactionType.AUCTION_DEPOSIT_LOCK => "Tạm khóa tiền đặt cọc đấu giá",
        AuctionTransactionType.AUCTION_PROGRESSIVE_DEPOSIT_LOCK => "Tạm khóa tiền cọc bổ sung lũy tiến",
        AuctionTransactionType.AUCTION_DEPOSIT_RELEASED => "Hoàn trả tiền cọc vào số dư khả dụng",
        AuctionTransactionType.AUCTION_COMPENSATION => "Nhận tiền bồi thường do đối phương vi phạm nghĩa vụ",
        AuctionTransactionType.AUCTION_DEPOSIT_FORFEITED => "Tịch thu tiền cọc do vi phạm quy chế đấu giá",
        AuctionTransactionType.ESCROW_HOLD => "Tạm giữ tiền thanh toán trúng đấu giá vào quỹ ký quỹ Escrow",
        AuctionTransactionType.ESCROW_DEBIT => "Khấu trừ thanh toán tiền mua SIM từ quỹ Escrow",
        AuctionTransactionType.ESCROW_REFUNDED => "Hoàn trả tiền từ quỹ ký quỹ Escrow",
        AuctionTransactionType.SELLER_PAYOUT => "Nhận tiền thanh toán chuyển nhượng SIM thành công",
        AuctionTransactionType.PENALTY => "Khấu trừ tiền phạt vi phạm",
        _ => "Giao dịch tài chính đấu giá"
    };
}
