namespace TrustSimBackend.Domain.Enums
{
    /// <summary>
    /// Loại giao dịch tài chính nội bộ trong các hoạt động đấu giá và ví.
    /// </summary>
    public enum AuctionTransactionType
    {
        /// <summary>Ghi nhận tiền nạp thành công vào ví từ ngân hàng.</summary>
        TOP_UP_CREDIT,

        /// <summary>Khóa số dư khả dụng khi người dùng tạo lệnh rút tiền.</summary>
        WITHDRAW_LOCK,

        /// <summary>Xác nhận rút tiền thành công về ngân hàng.</summary>
        WITHDRAW_COMPLETED,

        /// <summary>Rút tiền thất bại, hoàn trả lại số dư khả dụng.</summary>
        WITHDRAW_RELEASED,

        /// <summary>Khóa tiền cọc tham gia đấu giá (chuyển từ available sang locked).</summary>
        AUCTION_DEPOSIT_LOCK,

        /// <summary>Hoàn trả tiền cọc về số dư khả dụng sau khi phiên đấu giá kết thúc.</summary>
        AUCTION_DEPOSIT_RELEASED,

        /// <summary>Tịch thu tiền cọc do vi phạm quy chế đấu giá.</summary>
        AUCTION_DEPOSIT_FORFEITED,

        /// <summary>Giữ tiền thanh toán trúng đấu giá vào quỹ ký quỹ (Escrow).</summary>
        ESCROW_HOLD,

        /// <summary>Khấu trừ tiền từ quỹ ký quỹ để thanh toán.</summary>
        ESCROW_DEBIT,

        /// <summary>Hoàn trả tiền từ quỹ ký quỹ về ví khi phiên/hợp đồng bị hủy.</summary>
        ESCROW_REFUNDED,

        /// <summary>Thanh toán tiền bán SIM cho người bán.</summary>
        SELLER_PAYOUT,

        /// <summary>Khấu trừ tiền phạt vi phạm nghĩa vụ.</summary>
        PENALTY,

        /// <summary>Khóa tiền cọc bổ sung lũy tiến theo giá trúng đấu giá khi phiên kết thúc.</summary>
        AUCTION_PROGRESSIVE_DEPOSIT_LOCK
    }
}

