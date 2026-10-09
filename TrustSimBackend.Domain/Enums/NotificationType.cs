namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Phân loại thông báo hệ thống và nghiệp vụ đấu giá/tài chính.
/// </summary>
public enum NotificationType
{
    SYSTEM_ALERT = 0,
    AUCTION_WON = 1,
    AUCTION_LOST = 2,
    AUCTION_SELLER_CONFIRMATION_REQUIRED = 3,
    AUCTION_RUNNER_UP_OFFERED = 4,
    AUCTION_DEPOSIT_REFUNDED = 5,
    AUCTION_DEPOSIT_FORFEITED = 6,
    AUCTION_FAILED = 7,
    WALLET_TOPUP_SUCCESS = 8,
    WALLET_WITHDRAWAL_COMPLETED = 9,
    TRADE_TRANSFERRING_SIM = 10
}
