namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// Kết quả thử nghiệm mô phỏng can thiệp dữ liệu bất hợp pháp (Live Tamper Detection Demo).
/// Phục vụ trình diễn trước Hội đồng: chứng minh hệ thống ngay lập tức phát hiện khi DBA sửa lén 1 byte SQL.
/// </summary>
public class TamperSimulationResultDto
{
    /// <summary>
    /// ID của bản ghi mắt xích bị can thiệp thử nghiệm.
    /// </summary>
    public Guid TamperedEntryId { get; set; }

    /// <summary>
    /// Số thứ tự SequenceNumber của mắt xích bị sửa.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Số tiền gốc ban đầu (VNĐ).
    /// </summary>
    public decimal OriginalAmount { get; set; }

    /// <summary>
    /// Số tiền bị cố tình sửa đổi (VNĐ).
    /// </summary>
    public decimal TamperedAmount { get; set; }

    /// <summary>
    /// Trạng thái phát hiện của hệ thống: "RED - TAMPER DETECTED" hoặc "GREEN - VALID".
    /// </summary>
    public string DetectionStatus { get; set; } = string.Empty;

    /// <summary>
    /// Báo cáo chi tiết vị trí đứt gãy mắt xích ngay sau khi sửa.
    /// </summary>
    public LedgerIntegrityReportDto VerificationReport { get; set; } = new();

    /// <summary>
    /// Thông điệp kết luận dành cho Hội đồng đánh giá.
    /// </summary>
    public string ThesisExplanation { get; set; } = string.Empty;
}
