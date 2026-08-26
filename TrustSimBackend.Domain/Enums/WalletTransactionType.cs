namespace TrustSimBackend.Domain.Enums
{
    /// <summary>
    /// Loại giao dịch nạp / rút tiền qua ngân hàng.
    /// </summary>
    public enum WalletTransactionType
    {
        /// <summary>Nạp tiền vào ví từ tài khoản ngân hàng.</summary>
        TOP_UP,

        /// <summary>Rút tiền từ ví về tài khoản ngân hàng của người dùng.</summary>
        WITHDRAW
    }
}
