namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

public class AuctionExcelExportRow
{
    public string SimNumber { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string AuctionType { get; set; } = string.Empty;
    public decimal StartingPrice { get; set; }
    public decimal StepPrice { get; set; }
    public decimal CurrentHighestBid { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}
