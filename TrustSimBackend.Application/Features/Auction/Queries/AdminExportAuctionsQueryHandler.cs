using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class AdminExportAuctionsQueryHandler : IRequestHandler<AdminExportAuctionsQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public AdminExportAuctionsQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportAuctionsQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _unitOfWork.Auctions.GetPagedAsync(
            new AuctionQueryFilter
            {
                Keyword = request.Keyword,
                Status = request.Status,
                Type = request.Type,
                PageIndex = 1,
                PageSize = 100000
            },
            cancellationToken);

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
