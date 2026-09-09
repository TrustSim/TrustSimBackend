namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

public class TransactionExcelExportRow
{
    public string TransactionCode { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
