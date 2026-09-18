namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

/// <summary>
/// DTO thông tin nhà mạng kèm danh sách đầu số.
/// </summary>
public class SimCarrierDto
{
    public Guid Id { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string LogoImg { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public List<PhonePrefixDto> Prefixes { get; set; } = new();
}
