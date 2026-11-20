namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class ChatConsultationResponseDto
{
    public Guid SessionId { get; set; }
    public Guid MessageId { get; set; }
    public string ReplyContent { get; set; } = string.Empty;
    public List<ConsultationSimDto> SuggestedSims { get; set; } = new();
    public List<string> ReferencedDocuments { get; set; } = new();
    public List<Guid> ReferencedChunkIds { get; set; } = new();
    public string EngineSource { get; set; } = "NVIDIA_CLOUD_LLM";
    public string? ModelUsed { get; set; }
    public string Intent { get; set; } = "GeneralConsultation";
    public bool CacheHit { get; set; }
    public bool GuardrailBlocked { get; set; }
    public int TokensUsed { get; set; }
    public long ExecutionTimeMs { get; set; }
}
