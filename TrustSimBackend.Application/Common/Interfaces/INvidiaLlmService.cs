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
    /// Sinh phản hồi tư vấn SIM và gợi ý danh sách SIM phù hợp (Static context)
    /// </summary>
    Task<string> GenerateConsultationResponseAsync(
        string userMessage,
        string conversationHistory,
        IReadOnlyList<DocumentChunkSearchResult> ragFengShuiChunks,
        IReadOnlyList<ConsultationSimSuggestion> availableSims,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tư vấn chuyên sâu bằng cơ chế Agentic ReAct Tool Calling (NVIDIA/OpenAI Function Calling Protocol)
    /// LLM tự động quyết định gọi các Repository (Kho SIM, RAG, Bóc tách nhịp) theo đúng nhu cầu người dùng.
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
    /// Tư vấn bằng cơ chế Agentic ReAct Tool Calling phát luồng SSE từng token theo thời gian thực (ChatGPT-style Streaming)
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
