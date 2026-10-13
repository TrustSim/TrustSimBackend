namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Cấu hình công thức tính tiền cọc Người Bán (Seller Listing Deposit) nạp từ appsettings.json.
/// Công thức: Deposit = Clamp(StartingPrice * DepositRate, MinDepositAmount, MaxDepositAmount).
/// </summary>
public class SellerDepositSettings
{
    public const string SectionName = "SellerDepositSettings";

    /// <summary>
    /// Tỷ lệ cọc mặc định theo Giá khởi điểm (Mặc định: 10% = 0.10).
    /// </summary>
    public decimal DepositRate { get; set; } = 0.10m;

    /// <summary>
    /// Số tiền cọc sàn tối thiểu (Mặc định: 50,000 VNĐ).
    /// </summary>
    public decimal MinDepositAmount { get; set; } = 50_000m;

    /// <summary>
    /// Số tiền cọc trần tối đa (Mặc định: 5,000,000 VNĐ).
    /// </summary>
    public decimal MaxDepositAmount { get; set; } = 5_000_000m;
}
