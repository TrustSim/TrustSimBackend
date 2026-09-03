namespace TrustSimBackend.Application.Features.Sim.DTOs;

public class PhonePrefixDto
{
    public Guid Id { get; set; }
    public string PrefixValue { get; set; } = string.Empty;
    public Guid SimCarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
}

public record CreatePrefixRequest(
    string PrefixValue,
    Guid SimCarrierId
);
