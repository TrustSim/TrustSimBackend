namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class WalletExcelExportRow
{
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public decimal AvailableBalance { get; set; }
    public decimal LockedBalance { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
