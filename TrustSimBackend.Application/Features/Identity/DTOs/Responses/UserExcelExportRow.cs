namespace TrustSimBackend.Application.Features.Identity.DTOs.Responses;

public class UserExcelExportRow
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string IdentityNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string IsActive { get; set; } = string.Empty;
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public string CreatedDate { get; set; } = string.Empty;
}
