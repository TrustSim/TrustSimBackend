namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu hoàn tiền cọc đấu giá cho người không trúng (Nghiệp vụ 6).
/// </summary>
public class ReleaseAuctionDepositRequest
{
    public Guid UserId { get; set; }
    public decimal AmountToRelease { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? AuctionDepositId { get; set; }
    public string? Reason { get; set; }
}
