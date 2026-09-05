using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Responses;

public class TradeTransactionDto
{
    public Guid Id { get; set; }
    public string TradeTransactionCode { get; set; } = string.Empty;
    public Guid AuctionId { get; set; }
    public Guid BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public Guid SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public Guid SimNumberId { get; set; }
    public string SimPhoneNumber { get; set; } = string.Empty;
    public decimal EscrowAmount { get; set; }
    public TradeTransactionStatus Status { get; set; }
    public EscrowStatus EscrowStatus { get; set; }
    public DateTime EscrowDeadline { get; set; }
    public DateTime CreatedDate { get; set; }
}
