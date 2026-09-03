namespace TrustSimBackend.Application.Features.System.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

public record CreateDocumentRequest(
    string DocumentName,
    string LinkUrl
);

public record UpdateDocumentRequest(
    string DocumentName,
    string LinkUrl
);
