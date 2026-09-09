namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

public class TradeExcelExportRow
{
    public string TransactionId { get; set; } = string.Empty;
    public string SimNumber { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal DepositAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
