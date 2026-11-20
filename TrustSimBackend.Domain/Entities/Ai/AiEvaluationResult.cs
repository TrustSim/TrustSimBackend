using TrustSimBackend.Domain.Base;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Persisted automated evaluation result (online auto-eval or benchmark suite) linked to an <see cref="AiTelemetryLog"/>.
/// </summary>
public class AiEvaluationResult : FullAuditableEntity
{
    public Guid TelemetryLogId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? MessageId { get; set; }

    public string EvalType { get; set; } = "ONLINE_AUTO";
    public string? TestCaseCode { get; set; }

    public double GroundednessScore { get; set; }
    public double RelevanceScore { get; set; }
    public double SafetyScore { get; set; }
    public double ToolAccuracyScore { get; set; }
    public double OverallScore { get; set; }
    public bool Passed { get; set; }

    public string? HallucinationFlagsJson { get; set; }
    public string? EvaluationNotes { get; set; }

    public virtual AiTelemetryLog? TelemetryLog { get; set; }
}
