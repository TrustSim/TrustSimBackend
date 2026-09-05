namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

public class SimCategoryDto
{
    public Guid Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float ValuationMultiplier { get; set; }
}
