namespace TrustSimBackend.Application.Features.Sim.DTOs;

/// <summary>
/// DTO thông tin nhà mạng kèm danh sách đầu số.
/// </summary>
public class SimCarrierDto
{
    public Guid Id { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string LogoImg { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<PhonePrefixDto> Prefixes { get; set; } = new();
}

/// <summary>
/// DTO thông tin đầu số điện thoại.
/// </summary>
public class PhonePrefixDto
{
    public Guid Id { get; set; }
    public string PrefixValue { get; set; } = string.Empty;
    public Guid SimCarrierId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
