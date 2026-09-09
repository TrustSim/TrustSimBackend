using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string AuditLogName { get; set; } = string.Empty;
    public ActionType ActionType { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public Guid ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string OldData { get; set; } = string.Empty;
    public string NewData { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
