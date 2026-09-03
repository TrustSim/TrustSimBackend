namespace TrustSimBackend.Application.Features.Sim.DTOs;

public class SimCarrierDto
{
    public Guid Id { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string LogoImg { get; set; } = string.Empty;
    public List<PhonePrefixDto> Prefixes { get; set; } = new();
}

public record CreateCarrierRequest(
    string CarrierName,
    string CarrierCode,
    string? LogoImg
);

public record UpdateCarrierRequest(
    string CarrierName,
    string? LogoImg
);
