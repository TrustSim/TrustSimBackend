using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Persisted telemetry record for every AI chatbot, RAG, MCP tool, guardrail, and valuation execution.
/// </summary>
public class AiTelemetryLog : FullAuditableEntity
{
    public Guid? SessionId { get; set; }
    public Guid? MessageId { get; set; }
    public Guid? UserAccountId { get; set; }

    public string OperationType { get; set; } = "CONSULTATION_CHAT";
    public string Intent { get; set; } = "GeneralConsultation";
    public string? ModelUsed { get; set; }
    public string EngineSource { get; set; } = "SEMANTIC_KERNEL_GEMINI";

    public bool CacheHit { get; set; }
    public string CacheLayer { get; set; } = "MISS";

    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }

    public long TotalLatencyMs { get; set; }
    public long RagLatencyMs { get; set; }
    public long ToolLatencyMs { get; set; }
    public long LlmLatencyMs { get; set; }

    public int ToolCallCount { get; set; }
    public string? ToolsInvokedJson { get; set; }
    public string? ReferencedChunkIdsJson { get; set; }

    public bool GuardrailBlocked { get; set; }
    public string? GuardrailReason { get; set; }
    public bool PermissionDenied { get; set; }
    public string? DeniedToolName { get; set; }

    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? UserPromptPreview { get; set; }

    public virtual ICollection<AiEvaluationResult> EvaluationResults { get; set; } = new List<AiEvaluationResult>();
}
