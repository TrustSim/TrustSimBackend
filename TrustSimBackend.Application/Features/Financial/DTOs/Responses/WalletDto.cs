using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class WalletDto
{
    public Guid Id { get; set; }
    public Guid UserAccountId { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public decimal TotalBalance => AvailableBalance + LockedBalance;
    public WalletStatus WalletStatus { get; set; }
    public int LockVersion { get; set; }
}
