using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs;

/// <summary>
/// DTO chứa thông tin tổng quan về ví tiền của người dùng.
/// </summary>
public class WalletDto
{
    public Guid WalletId { get; set; }
    public Guid UserAccountId { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public decimal TotalBalance => AvailableBalance + LockedBalance;
    public WalletStatus WalletStatus { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
