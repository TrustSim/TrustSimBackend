namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Yêu cầu mô phỏng can thiệp dữ liệu bất hợp pháp để Demo Hội đồng.
/// </summary>
public class SimulateTamperRequest
{
    /// <summary>
    /// Số thứ tự SequenceNumber của mắt xích muốn can thiệp sửa đổi (Để 0 để tự chọn mắt xích gần nhất).
    /// </summary>
    public long SequenceNumber { get; set; } = 0;

    /// <summary>
    /// Số tiền giả mạo mới muốn gán vào cột Amount (Mặc định: 99.000.000 VNĐ).
    /// </summary>
    public decimal TamperedAmount { get; set; } = 99_000_000m;
}
