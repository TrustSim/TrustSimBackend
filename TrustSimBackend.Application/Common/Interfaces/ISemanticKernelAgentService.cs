using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Giao diện dịch vụ Core AI Agent điều phối bởi Microsoft Semantic Kernel kết nối Google Gemini API.
/// </summary>
public interface ISemanticKernelAgentService
{
    /// <summary>
    /// Thẩm định giá SIM chuyên sâu bằng mô hình Hybrid 4 tầng kết hợp Google Gemini qua Semantic Kernel.
    /// </summary>
    Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tư vấn SIM thông minh với Automatic Function Calling kích hoạt 6 Native Plugins.
    /// </summary>
    Task<AiConsultationLlmResponse> GenerateConsultationWithAgenticToolsAsync(
        string userMessage,
        string conversationHistory,
        int? userBirthYear = null,
        string? userCareer = null,
        string? fengShuiElement = null,
        decimal? budgetMin = null,
        decimal? budgetMax = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streaming SSE Token theo thời gian thực kết hợp bắt các sự kiện kích hoạt công cụ (tool_start, tool_end).
    /// </summary>
    IAsyncEnumerable<ChatConsultationStreamEvent> StreamConsultationWithAgenticToolsAsync(
        string userMessage,
        string conversationHistory,
        int? userBirthYear = null,
        string? userCareer = null,
        string? fengShuiElement = null,
        decimal? budgetMin = null,
        decimal? budgetMax = null,
        CancellationToken cancellationToken = default);
}
