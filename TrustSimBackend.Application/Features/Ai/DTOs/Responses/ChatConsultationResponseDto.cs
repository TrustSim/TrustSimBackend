namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class ChatConsultationResponseDto
{
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public string ReplyContent { get; set; } = string.Empty;
    public List<ConsultationSimDto> SuggestedSims { get; set; } = new();
    public List<string> ReferencedDocuments { get; set; } = new();
    public string EngineSource { get; set; } = "NVIDIA_CLOUD_LLM";
    public long ExecutionTimeMs { get; set; }
}
