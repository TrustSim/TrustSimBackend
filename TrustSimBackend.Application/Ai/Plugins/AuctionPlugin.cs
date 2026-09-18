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

    [KernelFunction, Description("Lấy danh sách các phiên đấu giá SIM số đẹp VIP đang diễn ra trực tiếp trên sàn TrustSim. Gọi khi khách hỏi về đấu giá hoặc tìm sim đang đấu giá.")]
    public async Task<string> GetActiveAuctionsAsync(
        [Description("Giới hạn số lượng phiên đấu giá trả về (mặc định: 5)")] int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var result = await _auctionRepo.GetPublicPagedAsync(
            pageIndex: 1,
            pageSize: limit,
            status: AuctionStatus.ACTIVE,
            cancellationToken: cancellationToken);

        if (result.TotalCount == 0 || result.Items.Count == 0)
        {
            return "{\"total\":0,\"items\":[],\"message\":\"Hiện tại không có phiên đấu giá nào đang diễn ra trên sàn.\"}";
        }

        return JsonSerializer.Serialize(new
        {
            total = result.TotalCount,
            items = result.Items.Select(a => new
            {
                auctionId = a.Id,
                auctionCode = a.AuctionCode,
                phoneNumber = a.SimNumber?.PhoneNumber,
                startingPrice = a.StartingPrice,
                currentPrice = a.CurrentPrice,
                stepPrice = a.AuctionRule?.MinimumBidIncrement ?? 0,
                status = a.AuctionStatus.ToString(),
                endTime = a.EndAt.ToString("yyyy-MM-dd HH:mm:ss")
            })
        });
    }
}
