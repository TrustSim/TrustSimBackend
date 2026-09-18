namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

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
    public bool IsActive { get; set; } = true;
}
