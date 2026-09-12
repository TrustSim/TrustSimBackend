namespace TrustSimBackend.Application.Features.Sim.DTOs;

/// <summary>
/// DTO danh mục phân loại số SIM (Tứ quý, Thần tài, Sảnh tiến...) và hệ số định giá.
/// </summary>
public class SimCategoryDto
{
    public Guid Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float ValuationMultiplier { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO kiểu nhịp định dạng số SIM (Sảnh tiến, Lặp đôi, Gánh...).
/// </summary>
public class FormatRhythmDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string FormatRhythmName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
