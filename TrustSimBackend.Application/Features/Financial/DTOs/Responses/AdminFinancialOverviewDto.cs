namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO Báo cáo Tổng quan Tài chính toàn sàn dành cho Ban Quản Trị (Admin).
/// </summary>
public class AdminFinancialOverviewDto
{
    /// <summary>Tổng tiền thực tế tại tài khoản ngân hàng của Sàn (TK 1010 EXTERNAL_GATEWAY_CLEARING)</summary>
    public decimal TotalBankBalance { get; set; }

    /// <summary>Phân rã toàn bộ nghĩa vụ nợ của sàn đối với khách hàng (Liabilities)</summary>
    public CustomerFundsBreakdownDto UserObligations { get; set; } = new();

    /// <summary>Phân rã doanh thu, tiền phạt và lợi nhuận khả dụng có thể rút của sàn</summary>
    public PlatformRevenueBreakdownDto PlatformRevenue { get; set; } = new();

    /// <summary>Tóm tắt trạng thái toàn vẹn sổ cái và cân bằng kế toán</summary>
    public FinancialIntegritySummaryDto Integrity { get; set; } = new();
}

/// <summary>
/// Chi tiết nguồn tiền của khách hàng đang nằm tại sàn.
/// </summary>
public class CustomerFundsBreakdownDto
{
    /// <summary>Tổng số dư khả dụng trong ví của tất cả người dùng (TK 2010)</summary>
    public decimal UserAvailableBalance { get; set; }

    /// <summary>Tổng số dư tiền cọc đang bị khóa tạm thời của người dùng (TK 2020)</summary>
    public decimal UserLockedBalance { get; set; }

    /// <summary>Tổng tiền ký quỹ Escrow đang giữ trung gian chờ chuyển giao SIM (TK 2030)</summary>
    public decimal PlatformEscrowBalance { get; set; }

    /// <summary>Tổng nghĩa vụ tiền của khách hàng mà sàn có trách nhiệm hoàn trả/chi trả (2010 + 2020 + 2030)</summary>
    public decimal TotalCustomerFunds { get; set; }
}

/// <summary>
/// Chi tiết doanh thu và hạn mức rút lợi nhuận của Sàn.
/// </summary>
public class PlatformRevenueBreakdownDto
{
    /// <summary>Tổng doanh thu hoa hồng dịch vụ bán SIM 5% tích lũy (TK 4010)</summary>
    public decimal TotalFeeRevenueCollected { get; set; }

    /// <summary>Tổng doanh thu tiền phạt người chơi vi phạm quy chế bỏ cọc 10% tích lũy (TK 4020)</summary>
    public decimal TotalPenaltyRevenueCollected { get; set; }

    /// <summary>Tổng doanh thu gộp của sàn (Hoa hồng + Tiền phạt)</summary>
    public decimal TotalGrossRevenue { get; set; }

    /// <summary>Tổng số tiền lợi nhuận Admin đã rút trước đó</summary>
    public decimal TotalProfitWithdrawn { get; set; }

    /// <summary>Số tiền lợi nhuận sàn KHẢ DỤNG hiện tại có thể rút về STK cá nhân</summary>
    public decimal AvailableProfitForWithdrawal { get; set; }
}

/// <summary>
/// Trạng thái bảo toàn toàn vẹn tài chính.
/// </summary>
public class FinancialIntegritySummaryDto
{
    /// <summary>Phương trình kế toán cân bằng tuyệt đối (Tài sản Ngân hàng == Nợ khách hàng + Lợi nhuận sàn còn lại)</summary>
    public bool IsAccountingEquationBalanced { get; set; }

    /// <summary>Chuỗi khối SHA-256 & Chữ ký số ECDsa hợp lệ 100%</summary>
    public bool IsChainValid { get; set; }

    /// <summary>Thời điểm kiểm toán mới nhất (UTC)</summary>
    public DateTime LastAuditedAt { get; set; } = DateTime.UtcNow;
}
