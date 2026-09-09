namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public class CategoryExcelRow
{
    public string? CategoryCode { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }
    public float ValuationMultiplier { get; set; } = 1.0f;
}
