using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

/// <summary>
/// Handler lấy danh sách giao dịch chuyển nhượng SIM có hồ sơ bằng chứng chờ Admin duyệt (dạng phẳng và gom nhóm theo Phiên đấu giá).
/// </summary>
public class GetPendingTransferReviewsQueryHandler :
    IRequestHandler<GetPendingTransferReviewsQuery, List<PendingTransferReviewDto>>,
    IRequestHandler<GetPendingTransferReviewsGroupedByAuctionQuery, List<AuctionPendingTransferReviewGroupDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetPendingTransferReviewsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<PendingTransferReviewDto>> Handle(
        GetPendingTransferReviewsQuery request,
        CancellationToken cancellationToken)
    {
        var trades = await _unitOfWork.TradeTransactions.GetPendingReviewsAsync(cancellationToken);
        var filteredTrades = FilterTrades(trades, request.AuctionId, request.Keyword);
        return MapToPendingReviewDtos(filteredTrades);
    }

    public async Task<List<AuctionPendingTransferReviewGroupDto>> Handle(
        GetPendingTransferReviewsGroupedByAuctionQuery request,
        CancellationToken cancellationToken)
    {
        var trades = await _unitOfWork.TradeTransactions.GetPendingReviewsAsync(cancellationToken);
        var filteredTrades = FilterTrades(trades, request.AuctionId, request.Keyword);
        var mappedDtos = MapToPendingReviewDtos(filteredTrades);

        var tradeLookup = filteredTrades.ToDictionary(t => t.Id);

        var grouped = mappedDtos
            .GroupBy(d => d.AuctionId)
            .Select(group =>
            {
                var firstDto = group.First();
                tradeLookup.TryGetValue(firstDto.TradeId, out var firstTradeEntity);
                var auction = firstTradeEntity?.Auction;

                var allSellerProofs = group
                    .SelectMany(d => d.SellerProofs)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToList();

                var allBuyerProofs = group
                    .SelectMany(d => d.BuyerProofs)
                    .OrderByDescending(p => p.CreatedDate)
                    .ToList();

                var pendingSellerCount = group.Sum(d => d.PendingSellerProofCount);
                var pendingBuyerCount = group.Sum(d => d.PendingBuyerProofCount);

                return new AuctionPendingTransferReviewGroupDto(
                    AuctionId: group.Key,
                    AuctionCode: firstDto.AuctionCode,
                    AuctionType: firstDto.AuctionType,
                    AuctionStatus: firstDto.AuctionStatus,
                    SimPhoneNumber: firstDto.SimPhoneNumber,
                    WinningPrice: firstDto.WinningPrice,
                    AuctionStartAt: auction?.StartAt,
                    AuctionEndAt: auction?.EndAt,
                    TotalTradesCount: group.Count(),
                    TotalPendingProofsCount: pendingSellerCount + pendingBuyerCount,
                    PendingSellerProofCount: pendingSellerCount,
                    PendingBuyerProofCount: pendingBuyerCount,
                    HasApprovedSeller: group.Any(d => d.HasApprovedSeller),
                    HasApprovedBuyer: group.Any(d => d.HasApprovedBuyer),
                    SellerProofs: allSellerProofs,
                    BuyerProofs: allBuyerProofs,
                    Trades: group.ToList()
                );
            })
            .ToList();

        return grouped;
    }

    private static List<TradeTransaction> FilterTrades(
        IReadOnlyList<TradeTransaction> trades,
        Guid? auctionId,
        string? keyword)
    {
        IEnumerable<TradeTransaction> query = trades;

        if (auctionId.HasValue && auctionId.Value != Guid.Empty)
        {
            query = query.Where(t => t.AuctionId == auctionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var cleanKeyword = keyword.Trim();
            query = query.Where(t =>
            {
                var auctionCode = t.Auction?.AuctionCode ?? string.Empty;
                var tradeCode = t.TradeTransactionCode ?? string.Empty;
                var simPhone = t.SimNumber?.PhoneNumber ?? string.Empty;
                var sellerName = t.Seller?.UserProfile?.FullName ?? t.Seller?.UserName ?? string.Empty;
                var buyerName = t.Buyer?.UserProfile?.FullName ?? t.Buyer?.UserName ?? string.Empty;

                return auctionCode.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)
                    || tradeCode.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)
                    || simPhone.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)
                    || sellerName.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)
                    || buyerName.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase);
            });
        }

        return query.ToList();
    }

    private static List<PendingTransferReviewDto> MapToPendingReviewDtos(List<TradeTransaction> trades)
    {
        var result = new List<PendingTransferReviewDto>(trades.Count);

        foreach (var trade in trades)
        {
            var proofs = trade.SimTransfer?.TransferProofs ?? (IReadOnlyCollection<TransferProof>)Array.Empty<TransferProof>();

            var sellerProofs = proofs
                .Where(p => p.ProofType == TransferProofType.SELLER_HANDOVER)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new TransferProofDto(
                    p.Id,
                    p.ImageUrl,
                    p.Description,
                    p.ProofStatus.ToString(),
                    p.VerifiedAt,
                    p.CreatedDate,
                    p.ProofType.ToString()
                )).ToList();

            var buyerProofs = proofs
                .Where(p => p.ProofType == TransferProofType.BUYER_RECEIPT)
                .OrderByDescending(p => p.CreatedDate)
                .Select(p => new TransferProofDto(
                    p.Id,
                    p.ImageUrl,
                    p.Description,
                    p.ProofStatus.ToString(),
                    p.VerifiedAt,
                    p.CreatedDate,
                    p.ProofType.ToString()
                )).ToList();

            var hasApprovedSeller = sellerProofs.Any(p => p.ProofStatus == ProofStatus.APPROVED.ToString());
            var hasApprovedBuyer = buyerProofs.Any(p => p.ProofStatus == ProofStatus.APPROVED.ToString());

            var pendingSellerCount = sellerProofs.Count(p => p.ProofStatus == ProofStatus.PENDING.ToString());
            var pendingBuyerCount = buyerProofs.Count(p => p.ProofStatus == ProofStatus.PENDING.ToString());

            var sellerName = trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName ?? "Người Bán";
            var buyerName = trade.Buyer?.UserProfile?.FullName ?? trade.Buyer?.UserName ?? "Người Mua";

            var escrowAmount = trade.Escrow?.EscrowAmount ?? 0m;
            var winningPrice = trade.Auction?.CurrentPrice > 0
                ? trade.Auction.CurrentPrice
                : escrowAmount;

            result.Add(new PendingTransferReviewDto(
                TradeId: trade.Id,
                TradeTransactionCode: trade.TradeTransactionCode,
                SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
                SellerId: trade.SellerId,
                SellerName: sellerName,
                BuyerId: trade.BuyerId,
                BuyerName: buyerName,
                EscrowAmount: escrowAmount,
                SimTransferStatus: (trade.SimTransfer?.SimTransferStatus ?? SimTransferStatus.PENDING_SELLER_ACTION).ToString(),
                SellerProofs: sellerProofs,
                BuyerProofs: buyerProofs,
                HasApprovedSeller: hasApprovedSeller,
                HasApprovedBuyer: hasApprovedBuyer,
                CreatedDate: trade.CreatedDate,
                LiquidationDocumentUrl: trade.TransferContract?.LiquidationDocumentUrl,
                AuctionId: trade.AuctionId,
                AuctionCode: trade.Auction?.AuctionCode ?? string.Empty,
                AuctionType: trade.Auction?.AuctionType.ToString() ?? string.Empty,
                AuctionStatus: trade.Auction?.AuctionStatus.ToString() ?? string.Empty,
                WinningPrice: winningPrice,
                PendingSellerProofCount: pendingSellerCount,
                PendingBuyerProofCount: pendingBuyerCount
            ));
        }

        return result;
    }
}
