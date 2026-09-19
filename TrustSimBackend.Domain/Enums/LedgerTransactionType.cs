namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại nghiệp vụ tài chính phát sinh chứng từ sổ cái kép.
/// </summary>
public enum LedgerTransactionType
{
    /// <summary>Nạp tiền vào ví qua SePay/Cổng thanh toán.</summary>
    TOP_UP = 1,

    /// <summary>Khóa tiền yêu cầu rút về ngân hàng (Pha 1).</summary>
    WITHDRAW_LOCK = 2,

    /// <summary>Rút tiền thành công - Chi trả qua ngân hàng (Pha 2A).</summary>
    WITHDRAW_COMPLETED = 3,

    /// <summary>Rút tiền thất bại - Tự động hoàn tiền về ví khả dụng (Pha 2B).</summary>
    WITHDRAW_REFUND = 4,

    /// <summary>Khóa tiền cọc tham gia đấu giá SIM.</summary>
    AUCTION_DEPOSIT_LOCK = 5,

    /// <summary>Hoàn trả tiền cọc khi không trúng đấu giá.</summary>
    AUCTION_DEPOSIT_RELEASED = 6,

    /// <summary>Tịch thu tiền cọc khi người dùng vi phạm quy chế đấu giá.</summary>
    AUCTION_DEPOSIT_FORFEITED = 7,

    /// <summary>Khóa tiền ký quỹ Escrow sau khi thắng đấu giá.</summary>
    ESCROW_HOLD = 8,

    /// <summary>Giải ngân tiền bán SIM cho người bán sau khi hoàn tất giao dịch và trừ phí sàn.</summary>
    ESCROW_RELEASE = 9,

    /// <summary>Hoàn tiền ký quỹ Escrow về cho người mua khi hợp đồng hủy.</summary>
    ESCROW_REFUND = 10,

    /// <summary>Bút toán điều chỉnh số dư kế toán khẩn cấp từ Admin.</summary>
    ADMIN_ADJUSTMENT = 11
}
