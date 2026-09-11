using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class AdminWalletDetailDto
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public WalletStatus WalletStatus { get; set; }
    public DateTime CreatedDate { get; set; }
}
