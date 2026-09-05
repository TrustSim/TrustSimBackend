namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

public class SimCarrierDto
{
    public Guid Id { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string LogoImg { get; set; } = string.Empty;
}
