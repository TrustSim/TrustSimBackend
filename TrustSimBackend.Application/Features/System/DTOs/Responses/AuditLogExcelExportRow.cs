namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class AuditLogExcelExportRow
{
    public string LogName { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string CreatedDate { get; set; } = string.Empty;
}
