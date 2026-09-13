using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

/// <summary>
/// DTO biểu diễn quyền sở hữu SIM trong danh sách kho SIM của người dùng.
/// </summary>
public class SimOwnershipDto
{
    public Guid Id { get; set; }
    public Guid SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0m;
    public Guid? FormatRhythmId { get; set; }
    public string? FormatRhythmName { get; set; }
    public SimStatus SimStatus { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public string CarrierLogo { get; set; } = string.Empty;
    public OwnershipStatus OwnershipStatus { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedDate { get; set; }
}
