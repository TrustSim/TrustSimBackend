namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu khóa tiền cọc tham gia đấu giá (Nghiệp vụ 5).
/// </summary>
public class LockAuctionDepositRequest
{
    public Guid UserId { get; set; }
    public decimal AmountToLock { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? AuctionDepositId { get; set; }
    public string? Description { get; set; }
}
