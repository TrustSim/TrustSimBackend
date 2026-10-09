namespace TrustSimBackend.Domain.Enums
{
    public enum WalletStatus
    {
        /// <summary>Ví hoạt động bình thường (Cho phép Nạp, Rút, Đấu giá).</summary>
        ACTIVE,

        /// <summary>Ví bị đóng băng tạm thời (Cho phép Nạp Top-up IN để bổ sung ký quỹ, Chặn Rút Withdraw OUT, Cho phép Đấu giá).</summary>
        FROZEN,

        /// <summary>Ví bị khóa toàn phần do vi phạm hoặc điều tra (Chặn toàn bộ Nạp, Rút, Đấu giá).</summary>
        LOCKED
    }
}