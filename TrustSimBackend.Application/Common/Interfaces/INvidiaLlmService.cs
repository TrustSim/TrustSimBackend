using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ kết nối và gọi mô hình NVIDIA LLM (meta/llama-3.3-70b-instruct / nemotron-70b)
/// </summary>
public interface INvidiaLlmService
{
    /// <summary>
    /// Gửi prompt và nhận phản hồi văn bản đơn thuần từ LLM
    /// </summary>
    Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        float temperature = 0.3f,
        int maxTokens = 2048,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh kết quả định giá SIM có cấu trúc JSON từ LLM
    /// </summary>
    Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sinh phản hồi tư vấn SIM và gợi ý danh sách SIM phù hợp
    /// </summary>
    Task<string> GenerateConsultationResponseAsync(
        string userMessage,
        string conversationHistory,
        IReadOnlyList<DocumentChunkSearchResult> ragFengShuiChunks,
        IReadOnlyList<ConsultationSimSuggestion> availableSims,
        CancellationToken cancellationToken = default);
}
