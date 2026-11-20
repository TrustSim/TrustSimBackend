using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Records AI telemetry and automated evaluations to SQL database and serves observability/benchmark reports.
/// </summary>
public interface IAiTelemetryAndEvalService
{
    Task<Guid> RecordInteractionWithAutoEvalAsync(AiTelemetryRecordInput input, CancellationToken cancellationToken = default);
    Task<AiTelemetryDashboardDto> GetDashboardSummaryAsync(int lastHours = 24, CancellationToken cancellationToken = default);
    Task<PagedResult<AiEvaluationItemDto>> GetEvaluationResultsAsync(string? evalType, bool? passed, int pageIndex, int pageSize, CancellationToken cancellationToken = default);
    Task<AiBenchmarkRunReportDto> RunBenchmarkSuiteAsync(CancellationToken cancellationToken = default);
}
