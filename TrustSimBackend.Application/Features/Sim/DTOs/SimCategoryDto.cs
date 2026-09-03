namespace TrustSimBackend.Application.Features.Sim.DTOs;

public class SimCategoryDto
{
    public Guid Id { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public float ValuationMultiplier { get; set; }
}

public record CreateCategoryRequest(
    string CategoryCode,
    string CategoryName,
    string? Description,
    float ValuationMultiplier = 1.0f
);

public record UpdateCategoryRequest(
    string CategoryName,
    string? Description,
    float ValuationMultiplier
);
