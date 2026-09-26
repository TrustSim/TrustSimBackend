using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu thông tin các phiên đấu giá SIM VIP trực tiếp trên sàn TrustSim.
/// </summary>
public class AuctionPlugin
{
    private readonly IAuctionRepository _auctionRepo;

    public AuctionPlugin(IAuctionRepository auctionRepo)
    {
        _auctionRepo = auctionRepo;
    }

    [KernelFunction, Description("Lấy danh sách tất cả các phiên đấu giá SIM số đẹp VIP đang diễn ra (ACTIVE) hoặc lên lịch sắp diễn ra (SCHEDULE) trên sàn TrustSim. BẮT BUỘC gọi khi khách hỏi về đấu giá hoặc số lượng SIM đang đấu giá.")]
    public async Task<string> GetActiveAuctionsAsync(
        [Description("Trạng thái phiên đấu giá: ACTIVE, SCHEDULE, hoặc null để lấy toàn bộ")] string? status = null,
        CancellationToken cancellationToken = default)
    {
        AuctionStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AuctionStatus>(status.Trim(), true, out var parsed))
        {
            parsedStatus = parsed;
        }

        var result = await _auctionRepo.GetPublicPagedAsync(
            pageIndex: 1,
            pageSize: 100,
            status: parsedStatus,
            cancellationToken: cancellationToken);

        if (result.TotalCount == 0 || result.Items.Count == 0)
        {
            return "{\"total\":0,\"items\":[],\"message\":\"Hiện tại không có phiên đấu giá nào trên sàn.\"}";
        }

        return JsonSerializer.Serialize(new
        {
            total = result.TotalCount,
            items = result.Items.Select(a => new
            {
                auctionId = a.Id,
                auctionCode = a.AuctionCode,
                simNumberId = a.SimNumberId,
                phoneNumber = a.SimNumber?.PhoneNumber,
                carrier = a.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                startingPrice = a.StartingPrice,
                currentPrice = a.CurrentPrice,
                stepPrice = a.AuctionRule?.MinimumBidIncrement ?? 0,
                depositAmount = a.AuctionRule?.DepositAmount ?? (a.StartingPrice * 0.1m),
                status = a.AuctionStatus.ToString(),
                startAt = a.StartAt.ToString("yyyy-MM-dd HH:mm:ss"),
                endTime = a.EndAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
        });
    }

    [KernelFunction, Description("Lấy thông tin chi tiết một phiên đấu giá SIM VIP theo mã phiên đấu giá (AuctionCode).")]
    public async Task<string> GetAuctionDetailAsync(
        [Description("Mã phiên đấu giá (ví dụ: AUC123456)")] string auctionCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(auctionCode))
        {
            return "{\"error\":\"Vui lòng cung cấp mã phiên đấu giá hợp lệ.\"}";
        }

        var auction = await _auctionRepo.GetByCodeAsync(auctionCode.Trim(), cancellationToken);
        if (auction == null)
        {
            return "{\"error\":\"Không tìm thấy phiên đấu giá với mã này.\"}";
        }

        return JsonSerializer.Serialize(new
        {
            auctionId = auction.Id,
            auctionCode = auction.AuctionCode,
            phoneNumber = auction.SimNumber?.PhoneNumber,
            startingPrice = auction.StartingPrice,
            currentPrice = auction.CurrentPrice,
            stepPrice = auction.AuctionRule?.MinimumBidIncrement ?? 0,
            depositAmount = auction.AuctionRule?.DepositAmount ?? (auction.StartingPrice * 0.1m),
            status = auction.AuctionStatus.ToString(),
            type = auction.AuctionType.ToString(),
            totalBids = auction.Bids.Count,
            totalParticipants = auction.Participants.Count,
            startAt = auction.StartAt.ToString("yyyy-MM-dd HH:mm:ss"),
            endAt = auction.EndAt.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }
}


