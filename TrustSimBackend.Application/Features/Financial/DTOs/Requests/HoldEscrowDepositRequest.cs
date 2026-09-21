namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO yêu cầu ký quỹ Escrow bảo đảm chuyển nhượng SIM sau khi thắng đấu giá (Nghiệp vụ 8).
/// </summary>
public class HoldEscrowDepositRequest
{
    public Guid WinnerUserId { get; set; }
    public decimal TotalEscrowAmount { get; set; }
    public decimal ExistingDepositAmount { get; set; }
    public Guid EscrowId { get; set; }
    public string? Description { get; set; }
}
