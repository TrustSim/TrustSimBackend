using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class ConsultationMessageDto
{
    public Guid Id { get; set; }
    public ChatSenderRole SenderRole { get; set; }
    public string MessageContent { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}
