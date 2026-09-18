namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

/// <summary>
/// DTO kiểu nhịp định dạng số SIM (Sảnh tiến, Lặp đôi, Gánh...).
/// </summary>
public class FormatRhythmDto
{
    public Guid Id { get; set; }
    public string Value { get; set; } = string.Empty;
    public string FormatRhythmName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SimCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
}
