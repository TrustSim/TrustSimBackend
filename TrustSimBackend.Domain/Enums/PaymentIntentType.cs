namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại Payment Intent trong hệ thống tài chính.
/// </summary>
public enum PaymentIntentType
{
    /// <summary>Nạp tiền vào ví qua cổng thanh toán (SePay/VietQR).</summary>
    DEPOSIT = 1,

    /// <summary>Rút tiền từ ví về tài khoản ngân hàng cá nhân.</summary>
    WITHDRAW = 2,

    /// <summary>Thanh toán ký quỹ Escrow mua SIM sau đấu giá.</summary>
    ESCROW_PAYMENT = 3
}
