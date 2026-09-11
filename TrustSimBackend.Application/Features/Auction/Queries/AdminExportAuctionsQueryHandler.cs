using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class AdminExportAuctionsQueryHandler : IRequestHandler<AdminExportAuctionsQuery, byte[]>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IExcelService _excelService;

    public AdminExportAuctionsQueryHandler(IAuctionRepository auctionRepository, IExcelService excelService)
    {
        _auctionRepository = auctionRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportAuctionsQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _auctionRepository.GetPagedAsync(
            request.Keyword,
            request.Status,
            request.Type,
            null,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

        var rows = items.Select(a => new AuctionExcelExportRow
        {
            SimNumber = a.SimNumber?.PhoneNumber ?? string.Empty,
            SellerName = a.Author?.UserProfile?.FullName ?? a.Author?.UserName ?? string.Empty,
            AuctionType = a.AuctionType.ToString(),
            StartingPrice = a.StartingPrice,
            StepPrice = a.AuctionRule?.MinimumBidIncrement ?? 0m,
            CurrentHighestBid = a.CurrentPrice,
            Status = a.AuctionStatus.ToString(),
            StartTime = a.StartAt.ToString("yyyy-MM-dd HH:mm:ss"),
            EndTime = a.EndAt.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Auctions", cancellationToken: cancellationToken);
    }
}
