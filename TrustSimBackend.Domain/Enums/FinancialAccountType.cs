namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Hệ thống Tài khoản Kế toán Nội bộ (Chart of Accounts) cho Sổ cái Ghi kép TrustSim.
/// </summary>
public enum FinancialAccountType
{
    /// <summary>
    /// [Tài sản - 1010] Tiền thực tế tại tài khoản ngân hàng của sàn / Cổng trung gian SePay.
    /// </summary>
    EXTERNAL_GATEWAY_CLEARING = 1010,

    /// <summary>
    /// [Nợ phải trả - 2010] Tiền khả dụng của từng người dùng (WalletId), sàn có nghĩa vụ chi trả khi yêu cầu.
    /// </summary>
    USER_AVAILABLE = 2010,

    /// <summary>
    /// [Nợ phải trả - 2020] Tiền người dùng bị phong tỏa tạm thời (cọc đấu giá, chờ chuyển khoản rút tiền).
    /// </summary>
    USER_LOCKED = 2020,

    /// <summary>
    /// [Nợ phải trả trung gian - 2030] Tiền thanh toán mua SIM được giữ trung gian giữa Người mua &amp; Người bán.
    /// </summary>
    PLATFORM_ESCROW = 2030,

    /// <summary>
    /// [Doanh thu - 4010] Doanh thu phí sàn thu từ giao dịch bán SIM thành công hoặc phí dịch vụ.
    /// </summary>
    PLATFORM_FEE_REVENUE = 4010,

    /// <summary>
    /// [Doanh thu khác - 4020] Tiền phạt thu được khi người dùng vi phạm quy chế đấu giá (bỏ cọc, vi phạm hợp đồng).
    /// </summary>
    PLATFORM_PENALTY_REVENUE = 4020,

    /// <summary>
    /// [Vốn/Điều chỉnh - 5010] Tài khoản đối ứng xử lý các bút toán kiểm toán/điều chỉnh số dư khẩn cấp từ Admin.
    /// </summary>
    ADMIN_ADJUSTMENT = 5010
}
