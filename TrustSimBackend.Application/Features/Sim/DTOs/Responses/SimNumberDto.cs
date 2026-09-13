using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

public class SimNumberDto
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public SimStatus SimStatus { get; set; }
    public decimal Price { get; set; } = 0m;
    public Guid PhonePrefixId { get; set; }
    public string PrefixValue { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierLogo { get; set; } = string.Empty;
    public Guid? FormatRhythmId { get; set; }
    public List<string> CategoryNames { get; set; } = new();
    public DateTime CreatedDate { get; set; }
}
