using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository contract for persisting and querying AI telemetry logs and evaluation scores.
/// </summary>
public interface IAiTelemetryRepository
{
    Task AddTelemetryLogAsync(AiTelemetryLog log, CancellationToken cancellationToken = default);
    Task AddEvaluationResultAsync(AiEvaluationResult evaluation, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiTelemetryLog>> GetTelemetryLogsSinceAsync(
        DateTime fromUtc,
        int maxCount = 500,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<AiEvaluationResult> Items, int TotalCount)> GetPagedEvaluationResultsAsync(
        string? evalType,
        bool? passed,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
}
