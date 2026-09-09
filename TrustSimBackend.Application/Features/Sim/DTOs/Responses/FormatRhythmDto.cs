namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

public class FormatRhythmDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string FormatRhythmName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SimCount { get; set; }
    public DateTime CreatedDate { get; set; }
}
