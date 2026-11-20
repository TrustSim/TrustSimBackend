namespace TrustSimBackend.Application.Features.Ai.DTOs.Responses;

public class AiTelemetryDashboardDto
{
    public int WindowHours { get; set; }
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }

    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public double SuccessRate { get; set; }

    public int CacheHits { get; set; }
    public double CacheHitRate { get; set; }
    public int L1MemoryHits { get; set; }
    public int L2RedisHits { get; set; }

    public int GuardrailBlockedCount { get; set; }
    public int PermissionDeniedCount { get; set; }

    public long TotalPromptTokens { get; set; }
    public long TotalCompletionTokens { get; set; }
    public long TotalTokens { get; set; }

    public double AvgLatencyMs { get; set; }
    public long P95LatencyMs { get; set; }

    public double AvgOverallEvalScore { get; set; }
    public double AvgGroundednessScore { get; set; }
    public double AvgSafetyScore { get; set; }
    public double EvalPassRate { get; set; }

    public List<AiIntentBreakdownDto> IntentBreakdown { get; set; } = new();
    public List<AiModelBreakdownDto> ModelBreakdown { get; set; } = new();
    public List<AiTelemetryLogItemDto> RecentLogs { get; set; } = new();
}

public class AiIntentBreakdownDto
{
    public string Intent { get; set; } = string.Empty;
    public int Count { get; set; }
    public double AvgLatencyMs { get; set; }
}

public class AiModelBreakdownDto
{
    public string ModelUsed { get; set; } = string.Empty;
    public int Count { get; set; }
    public long TotalTokens { get; set; }
}

public class AiTelemetryLogItemDto
{
    public Guid Id { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? UserAccountId { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string Intent { get; set; } = string.Empty;
    public string? ModelUsed { get; set; }
    public string EngineSource { get; set; } = string.Empty;
    public bool CacheHit { get; set; }
    public string CacheLayer { get; set; } = string.Empty;
    public int TotalTokens { get; set; }
    public long TotalLatencyMs { get; set; }
    public int ToolCallCount { get; set; }
    public bool GuardrailBlocked { get; set; }
    public bool PermissionDenied { get; set; }
    public string? DeniedToolName { get; set; }
    public bool IsSuccess { get; set; }
    public double? OverallEvalScore { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class AiEvaluationItemDto
{
    public Guid Id { get; set; }
    public Guid TelemetryLogId { get; set; }
    public Guid? SessionId { get; set; }
    public string EvalType { get; set; } = string.Empty;
    public string? TestCaseCode { get; set; }
    public double GroundednessScore { get; set; }
    public double RelevanceScore { get; set; }
    public double SafetyScore { get; set; }
    public double ToolAccuracyScore { get; set; }
    public double OverallScore { get; set; }
    public bool Passed { get; set; }
    public string? EvaluationNotes { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class AiBenchmarkRunReportDto
{
    public DateTime ExecutedAtUtc { get; set; }
    public int TotalCases { get; set; }
    public int PassedCases { get; set; }
    public double PassRate { get; set; }
    public double AverageOverallScore { get; set; }
    public List<AiBenchmarkCaseResultDto> Cases { get; set; } = new();
}

public class AiBenchmarkCaseResultDto
{
    public string TestCaseCode { get; set; } = string.Empty;
    public string LayerFocus { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public double OverallScore { get; set; }
    public double SafetyScore { get; set; }
    public double GroundednessScore { get; set; }
    public double ToolAccuracyScore { get; set; }
    public long LatencyMs { get; set; }
    public string Notes { get; set; } = string.Empty;
}
