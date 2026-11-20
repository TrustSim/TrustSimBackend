using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Caller identity and access context propagated across all AI chatbot layers.
/// </summary>
public class AiUserContext
{
    public Guid? UserId { get; set; }
    public string? Role { get; set; }
    public bool IsAuthenticated => UserId.HasValue && UserId.Value != Guid.Empty;
    public string? ClientIp { get; set; }

    public string RateLimitIdentity =>
        IsAuthenticated
            ? $"user:{UserId!.Value:N}"
            : $"ip:{(string.IsNullOrWhiteSpace(ClientIp) ? "anonymous" : ClientIp.Trim())}";

    public static AiUserContext Anonymous(string? clientIp = null) => new()
    {
        UserId = null,
        Role = "GUEST",
        ClientIp = clientIp
    };
}

public enum AiIntentType
{
    GeneralConsultation = 0,
    SimSearch = 1,
    SimValuation = 2,
    MarketBenchmark = 3,
    FengShuiAndBirthYear = 4,
    Auction = 5,
    EscrowAndPolicy = 6,
    WalletAndDeposit = 7
}

public enum AiModelTier
{
    Fast = 0,
    Reasoning = 1
}

public enum McpToolPermissionLevel
{
    Anonymous = 0,
    Authenticated = 1,
    AdminOnly = 2
}

/// <summary>
/// Routing decision produced by the intent/model router layer.
/// </summary>
public class AiRoutingDecision
{
    public AiIntentType Intent { get; set; } = AiIntentType.GeneralConsultation;
    public AiModelTier ModelTier { get; set; } = AiModelTier.Fast;
    public IReadOnlyList<string> CandidateModels { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AllowedToolNames { get; set; } = Array.Empty<string>();
    public bool IsCacheable { get; set; }
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(30);
    public string RoutingReason { get; set; } = string.Empty;
}

public class AiInputGuardrailResult
{
    public bool IsAllowed { get; set; } = true;
    public bool IsRateLimited { get; set; }
    public bool IsInjectionDetected { get; set; }
    public bool IsLengthExceeded { get; set; }
    public string SanitizedMessage { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? SafeReplyContent { get; set; }
}

public class AiOutputGuardrailResult
{
    public string SanitizedContent { get; set; } = string.Empty;
    public bool WasRedacted { get; set; }
    public int RedactionCount { get; set; }
    public bool HasPotentialHallucination { get; set; }
    public List<string> SuspiciousPhoneNumbers { get; set; } = new();
}

public class AiTokenUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }

    public void Add(AiTokenUsage other)
    {
        PromptTokens += other.PromptTokens;
        CompletionTokens += other.CompletionTokens;
        TotalTokens += other.TotalTokens;
    }
}

/// <summary>
/// Metadata and JSON Schema 2020-12 descriptor for a single MCP tool.
/// </summary>
public class McpToolDescriptor
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object InputSchema { get; set; } = new { };
    public bool ReadOnlyHint { get; set; } = true;
    public McpToolPermissionLevel RequiredPermission { get; set; } = McpToolPermissionLevel.Anonymous;
    public IReadOnlyList<AiIntentType> SupportedIntents { get; set; } = Array.Empty<AiIntentType>();
}

public class McpToolExecutionResult
{
    public string ToolName { get; set; } = string.Empty;
    public bool IsError { get; set; }
    public bool PermissionDenied { get; set; }
    public string ContentJson { get; set; } = "{}";
    public string Summary { get; set; } = string.Empty;
    public long ExecutionTimeMs { get; set; }
    public List<ConsultationSimSuggestion> SuggestedSims { get; set; } = new();
    public List<Guid> ReferencedChunkIds { get; set; } = new();
    public List<string> ReferencedDocuments { get; set; } = new();
}

public class McpJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }

    [JsonPropertyName("_meta")]
    public Dictionary<string, object?>? Meta { get; set; }
}

public class McpJsonRpcError
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Data { get; set; }
}

public class McpJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    [JsonPropertyName("id")]
    public object? Id { get; set; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; set; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public McpJsonRpcError? Error { get; set; }
}

public class AiTelemetryRecordInput
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
    public List<string> ToolsInvoked { get; set; } = new();
    public List<Guid> ReferencedChunkIds { get; set; } = new();
    public bool GuardrailBlocked { get; set; }
    public string? GuardrailReason { get; set; }
    public bool PermissionDenied { get; set; }
    public string? DeniedToolName { get; set; }
    public bool IsSuccess { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string UserMessage { get; set; } = string.Empty;
    public string ReplyContent { get; set; } = string.Empty;
    public int SuggestedSimsCount { get; set; }
    public List<string> SuspiciousPhoneNumbers { get; set; } = new();
    public string EvalType { get; set; } = "ONLINE_AUTO";
    public string? TestCaseCode { get; set; }
}
