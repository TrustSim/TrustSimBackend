using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public NotificationType NotificationType { get; set; } = NotificationType.SYSTEM_ALERT;
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? MetadataJson { get; set; }
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedDate { get; set; }
}
