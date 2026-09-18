namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class ConsultationSimDto
{
    public Guid SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string HighlightReason { get; set; } = string.Empty;
}
