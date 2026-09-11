namespace TrustSimBackend.Application.Features.System.DTOs.Responses;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
