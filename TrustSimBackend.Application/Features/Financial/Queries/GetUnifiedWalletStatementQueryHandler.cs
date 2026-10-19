using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
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

        var walletTxs = await _unitOfWork.WalletTransactions.GetListByWalletIdAsync(wallet.Id, request.FromDate, request.ToDate, cancellationToken);
        var auctionTxs = await _unitOfWork.AuctionTransactions.GetByWalletIdAsync(wallet.Id, request.FromDate, request.ToDate, cancellationToken);

        var unifiedList = new List<UnifiedWalletTransactionDto>(walletTxs.Count + auctionTxs.Count);

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

        var totalCount = unifiedList.Count;
        var pagedItems = unifiedList
            .OrderByDescending(x => x.CreatedAt)
            .Skip((request.PageIndex - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToList();

        return new PagedResult<UnifiedWalletTransactionDto>(pagedItems, totalCount, request.PageIndex, request.PageSize);
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
