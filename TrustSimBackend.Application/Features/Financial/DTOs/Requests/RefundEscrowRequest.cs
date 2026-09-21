namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu hoàn trả 100% tiền ký quỹ Escrow về người mua (Nghiệp vụ 10).
/// </summary>
public class RefundEscrowRequest
{
    public Guid BuyerUserId { get; set; }
    public decimal TotalEscrowAmount { get; set; }
    public Guid EscrowId { get; set; }
    public string? RefundReason { get; set; }
}
