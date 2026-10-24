using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

/// <summary>
/// Query Handler lấy danh sách các giao dịch / phiên đấu giá đang cần người dùng xử lý quyết định hoặc thanh toán (phân trang, lọc, sắp xếp).
/// </summary>
public class GetMyPendingSettlementsQueryHandler : IRequestHandler<GetMyPendingSettlementsQuery, PagedResult<PendingSettlementDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetMyPendingSettlementsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<PendingSettlementDto>> Handle(GetMyPendingSettlementsQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        var availableBalance = wallet?.AvailableBalance ?? 0m;
        var now = DateTime.UtcNow;

        var pendingResults = await _unitOfWork.Auctions.GetPendingSettlementsForUserAsync(request.UserId, cancellationToken);
        var dtos = new List<PendingSettlementDto>();

        foreach (var r in pendingResults)
        {
            if (r.Auction == null) continue;

            var dto = new PendingSettlementDto
            {
                AuctionId = r.AuctionId,
                AuctionCode = r.Auction.AuctionCode,
                SimPhoneNumber = r.Auction.SimNumber?.PhoneNumber ?? string.Empty,
                CarrierName = r.Auction.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
                AuctionType = r.Auction.AuctionType,
                AuctionStatus = r.Auction.AuctionStatus,
                AuctionResultStatus = r.AuctionResultStatus,
                AvailableBalance = availableBalance,
                RunnerUpFinalPrice = r.RunnerUpFinalPrice,
                RunnerUpParticipantId = r.RunnerUpParticipantId
            };

            // Trường hợp 1: User là Người thắng cần thanh toán
            if (r.AuctionResultStatus == AuctionResultStatus.PENDING_PAYMENT &&
                r.Winner != null &&
                r.Winner.UserAccountId == request.UserId)
            {
                dto.Role = "WINNER";
                dto.ActionRequired = "PAY_SETTLEMENT";
                dto.FinalPrice = r.FinalPrice;

                var lockedDeposits = r.Winner.Deposits.Where(d => d.DepositStatus == DepositStatus.LOCKED).ToList();
                dto.DepositLocked = lockedDeposits.Sum(d => d.DepositAmount);
                dto.RemainingPaymentRequired = Math.Max(0, dto.FinalPrice - dto.DepositLocked);
                dto.DeficitAmount = Math.Max(0, dto.RemainingPaymentRequired - availableBalance);
                dto.CanDirectPay = dto.DeficitAmount == 0;
                dto.Deadline = r.PaymentDeadline;
                dto.RemainingSeconds = r.PaymentDeadline.HasValue ? Math.Max(0, (r.PaymentDeadline.Value - now).TotalSeconds) : null;

                dtos.Add(dto);
            }
            // Trường hợp 2: User là Á quân được trao quyền mua
            else if (r.AuctionResultStatus == AuctionResultStatus.RUNNER_UP_PENDING &&
                     r.RunnerUpParticipant != null &&
                     r.RunnerUpParticipant.UserAccountId == request.UserId)
            {
                dto.Role = "RUNNER_UP";
                dto.ActionRequired = "RESPOND_RUNNER_UP_OFFER";
                dto.FinalPrice = r.RunnerUpFinalPrice ?? 0m;

                var runnerUpLockedDeposits = r.RunnerUpParticipant.Deposits.Where(d => d.DepositStatus == DepositStatus.LOCKED).ToList();
                dto.DepositLocked = runnerUpLockedDeposits.Sum(d => d.DepositAmount);
                dto.RemainingPaymentRequired = Math.Max(0, dto.FinalPrice - dto.DepositLocked);
                dto.DeficitAmount = Math.Max(0, dto.RemainingPaymentRequired - availableBalance);
                dto.CanDirectPay = dto.DeficitAmount == 0;
                dto.Deadline = r.RunnerUpPaymentDeadline;
                dto.RemainingSeconds = r.RunnerUpPaymentDeadline.HasValue ? Math.Max(0, (r.RunnerUpPaymentDeadline.Value - now).TotalSeconds) : null;

                dtos.Add(dto);
            }
            // Trường hợp 3: User là Người bán cần duyệt / từ chối bán cho Á quân
            else if (r.AuctionResultStatus == AuctionResultStatus.PENDING_SELLER_CONFIRMATION &&
                     r.Auction.AuthorId == request.UserId)
            {
                dto.Role = "SELLER";
                dto.ActionRequired = "DECIDE_RUNNER_UP";
                dto.FinalPrice = r.FinalPrice;
                dto.DepositLocked = 0m;
                dto.RemainingPaymentRequired = 0m;
                dto.DeficitAmount = 0m;
                dto.CanDirectPay = true;
                dto.Deadline = r.SellerDecisionDeadline;
                dto.RemainingSeconds = r.SellerDecisionDeadline.HasValue ? Math.Max(0, (r.SellerDecisionDeadline.Value - now).TotalSeconds) : null;

                dtos.Add(dto);
            }
        }

        var filter = request.Filter;
        var query = dtos.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var cleanRole = filter.Role.Trim();
            query = query.Where(d => string.Equals(d.Role, cleanRole, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.ActionRequired))
        {
            var cleanAction = filter.ActionRequired.Trim();
            query = query.Where(d => string.Equals(d.ActionRequired, cleanAction, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var cleanKeyword = filter.Keyword.Trim();
            query = query.Where(d =>
                (d.AuctionCode != null && d.AuctionCode.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)) ||
                (d.SimPhoneNumber != null && d.SimPhoneNumber.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)) ||
                (d.CarrierName != null && d.CarrierName.Contains(cleanKeyword, StringComparison.OrdinalIgnoreCase)));
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(d => d.Deadline.HasValue && d.Deadline.Value >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(d => d.Deadline.HasValue && d.Deadline.Value <= filter.ToDate.Value);
        }

        if (filter.SortBy == PendingSettlementSortBy.RemainingPaymentRequired)
        {
            query = filter.SortOrder == SortOrder.Asc
                ? query.OrderBy(d => d.RemainingPaymentRequired)
                : query.OrderByDescending(d => d.RemainingPaymentRequired);
        }
        else if (filter.SortBy == PendingSettlementSortBy.FinalPrice)
        {
            query = filter.SortOrder == SortOrder.Asc
                ? query.OrderBy(d => d.FinalPrice)
                : query.OrderByDescending(d => d.FinalPrice);
        }
        else
        {
            query = filter.SortOrder == SortOrder.Asc
                ? query.OrderBy(d => d.Deadline ?? DateTime.MaxValue)
                : query.OrderByDescending(d => d.Deadline ?? DateTime.MinValue);
        }

        var totalCount = query.Count();
        var pagedItems = query
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return new PagedResult<PendingSettlementDto>(pagedItems, totalCount, filter.PageIndex, filter.PageSize);
    }
}
