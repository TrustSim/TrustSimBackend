namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class DocumentExcelExportRow
{
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
