namespace TrustSimBackend.Application.Features.Dispute.DTOs.Responses;

public class DisputeExcelExportRow
{
    public string TicketId { get; set; } = string.Empty;
    public string TradeTransactionId { get; set; } = string.Empty;
    public string ComplainantName { get; set; } = string.Empty;
    public string RespondentName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ResolutionResult { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
