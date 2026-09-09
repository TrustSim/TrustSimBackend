namespace TrustSimBackend.Application.Features.Sim.DTOs.Responses;

public class SimExcelExportRow
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string Categories { get; set; } = string.Empty;
    public string FormatRhythm { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
