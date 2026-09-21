namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu giải ngân Escrow cho người bán và thu phí sàn (Nghiệp vụ 9).
/// </summary>
public class ReleaseEscrowRequest
{
    public Guid SellerUserId { get; set; }
    public decimal TotalEscrowAmount { get; set; }
    public decimal PlatformFeePercent { get; set; } = 0.05m;
    public Guid EscrowId { get; set; }
    public string? Description { get; set; }
}
