namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu tịch thu tiền cọc đấu giá do vi phạm quy chế (Nghiệp vụ 7).
/// </summary>
public class ForfeitAuctionDepositRequest
{
    public Guid UserId { get; set; }
    public decimal AmountToForfeit { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? AuctionDepositId { get; set; }
    public string? ViolationReason { get; set; }
}
