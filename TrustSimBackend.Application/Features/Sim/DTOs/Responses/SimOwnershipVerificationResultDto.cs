using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

/// <summary>
/// DTO trả về kết quả xác thực quyền sở hữu SIM thành công.
/// </summary>
public class SimOwnershipVerificationResultDto
{
    public bool IsSuccess { get; set; }
    public Guid SimNumberId { get; set; }
    public Guid SimOwnershipId { get; set; }
    public Guid SimVerificationId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public decimal Price { get; set; } = 0m;
    public Guid? FormatRhythmId { get; set; }
    public string CarrierName { get; set; } = string.Empty;
    public string CarrierCode { get; set; } = string.Empty;
    public SimStatus SimStatus { get; set; }
    public OwnershipStatus OwnershipStatus { get; set; }
    public VerificationStatus VerificationStatus { get; set; }
    public DateTime VerifiedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
