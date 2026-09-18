using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Dịch vụ Core AI Agent sử dụng Microsoft Semantic Kernel kết nối Google Gemini API (Gemini 1.5 Flash).
/// Hỗ trợ Automatic Function Calling với 6 Native Plugins và Streaming SSE thời gian thực.
/// </summary>
public class SemanticKernelAgentService : ISemanticKernelAgentService
{
    private readonly Kernel _kernel;
    private readonly GeminiSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SemanticKernelAgentService> _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(25) };

    public SemanticKernelAgentService(
        Kernel kernel,
        IOptions<GeminiSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<SemanticKernelAgentService> logger)
    {
        _kernel = kernel;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Tự động kiểm tra nếu ragChunks rỗng, AI Agent tự gọi Tool để lấy RAG chunks từ Repository
        var effectiveChunks = ragChunks;
        if (effectiveChunks == null || effectiveChunks.Count == 0)
        {
            using var scope = _scopeFactory.CreateScope();
            var ragService = scope.ServiceProvider.GetService<IRagRetrievalService>();
            if (ragService != null)
            {
                var query = $"Thị trường định giá SIM số đẹp {features.CarrierName} đầu số {features.Prefix} thế số {features.DetectedRhythmName} {features.SelectedRhythmValue}";
                effectiveChunks = await ragService.RetrieveRelevantChunksAsync(query, topK: 3, minSimilarity: 0.3, cancellationToken);
            }
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng Bộ Định Giá Cục Bộ 4 Tầng.");
            var fallbackLocal = GenerateFallbackValuation(features, mathResult, effectiveChunks);
            fallbackLocal.EngineSource = "LOCAL_FALLBACK_ENGINE";
            fallbackLocal.ExecutionTimeMs = sw.ElapsedMilliseconds;
            return fallbackLocal;
        }

        var systemPrompt = @"Bạn là Chuyên gia Trưởng Hội đồng Thẩm định & Định giá SIM Viễn thông Độc lập tại Sàn Giao dịch TrustSim Việt Nam.
Nhiệm vụ của bạn là thẩm định giá trị thực tế của số SIM dựa trên:
1. Bóc tách 3 cụm cấu trúc: Đầu số (Nhà mạng & độ cổ), Thân giữa (Thế số gánh, lặp, tiến, đối xứng), Cụm đuôi (Thế số VIP, tài lộc, phong thủy), và Điểm Tổng Nút (1-10).
2. Tài liệu nghiên cứu thị trường, cẩm nang phong thủy và quy chuẩn định giá RAG cung cấp.
3. Mặt bằng giá giao dịch thực tế trên thị trường viễn thông Việt Nam theo 3 KÊNH GIAO DỊCH:
   - Kênh 1: Giá Mua Mới Đại Lý / Cửa Hàng (Retail Price - MaxPrice): Giá mua mới từ showroom/đại lý viễn thông có cam kết gói cước.
   - Kênh 2: Giá Thị Trường Tự Do P2P (Market P2P Price - SuggestedPrice): Giá sang tay, giao dịch chuyển nhượng tự do giữa người dùng với nhau.
   - Kênh 3: Giá Thương Lái / Thợ SIM Thu Mua Nhanh (Dealer Buyback Price - MinPrice): Giá thợ hoặc thương lái sẵn sàng thanh toán thu mua ngay trong 24h (chiết khấu 20% - 35% so với giá P2P).

QUY TẮC ĐỊNH GIÁ QUAN TRỌNG:
- Số thuê bao phổ thông, không có thế số VIP đặc biệt (ví dụ chỉ có cặp lặp thường như 77, không phải Tứ quý, Lục quý, Taxi, Sảnh tiến...) thì mức giá P2P chỉ dao động từ vài trăm nghìn đến dưới 1 triệu VNĐ (ví dụ 086.919.2776 giá P2P chỉ khoảng 350.000 - 500.000 VNĐ, thợ thu 200.000 - 300.000 VNĐ, đại lý bán 500.000 - 700.000 VNĐ).
- Không được định giá ảo hoặc thổi phồng giá của số phổ thông lên tiền triệu nếu không có thế số VIP.
- Đối với số SIM VIP (Bát quý, Lục quý, Ngũ quý, Tứ quý cổ, Sảnh rồng, Taxi 4 cặp...), hãy định giá sát với giá trị giao dịch tiền tỷ hoặc trăm triệu theo tri thức thị trường.

Hãy trả về kết quả thuần JSON tuân thủ cấu trúc sau (không kèm markdown ```json bọc ngoài):
{
  ""suggestedPrice"": 450000,
  ""minPrice"": 300000,
  ""maxPrice"": 650000,
  ""totalPoints"": 5,
  ""confidenceScore"": 0.95,
  ""marketMultiplier"": 1.0,
  ""rhythmRecommendation"": ""3.3.4"",
  ""formattedNumber"": ""086.919.2776"",
  ""categoryCodes"": [""GANH_DAO"", ""LOC_PHAT_86""],
  ""valuationReasoning"": ""1. Phân tích cấu trúc & thế số:\n- Đầu số 086: Thuộc nhà mạng Viettel mang ý nghĩa Phát Lộc...\n- Thân giữa 919: Cụm đối xứng gánh...\n- Cụm đuôi 2776: Thế số phụ...\n\n2. Tổng nút (Điểm số) & Phong thủy:\n- Tổng nút: 5 điểm...\n\n3. Phân cấp phân khúc thị trường:\n- Thuộc phân khúc SIM số phụ / phổ thông...\n\n4. Bảng định giá 3 kênh giao dịch:\n- Giá mua mới đại lý: ...\n- Giá sang tay tự do P2P: ...\n- Giá thợ thu mua thanh lý nhanh: ..."",
  ""fengShuiSummary"": ""Tóm tắt ngắn gọn ý nghĩa phong thủy và tổng nút...""
}";

        var ragContextBuilder = new StringBuilder();
        if (effectiveChunks != null && effectiveChunks.Count > 0)
        {
            int idx = 1;
            foreach (var chunk in effectiveChunks)
            {
                ragContextBuilder.AppendLine($"[Tài liệu tham khảo {idx++}: {chunk.DocumentName} (Độ tương đồng: {chunk.Similarity:P0})]");
                ragContextBuilder.AppendLine(chunk.Content);
                ragContextBuilder.AppendLine("---");
            }
        }
        else
        {
            ragContextBuilder.AppendLine("Áp dụng biểu giá thị trường chuẩn sàn TrustSim.");
        }

        var userPrompt = $@"THÔNG TIN SỐ SIM CẦN THẨM ĐỊNH:
- Số điện thoại gốc: {features.CleanPhoneNumber}
- Nhà mạng: {features.CarrierName} (Đầu số {features.Prefix})
- Cấu trúc 3 cụm: Đầu số [{features.PrefixGroup}], Thân giữa [{features.MiddleGroup}], Cụm đuôi [{features.TailGroup}]
- Tổng nút điểm số: {features.TotalPoints}/10 điểm
- Nhịp ngắt được hệ thống bóc tách tối ưu: {features.SelectedRhythmValue} ({features.DetectedRhythmName})
- Hiển thị theo nhịp tối ưu: {features.FormattedNumber}
- Các thế số & tổ hợp tài lộc bộc lộ: {(features.DetectedCategories.Count > 0 ? string.Join(", ", features.DetectedCategories.Select(c => $"{c.CategoryName} ({c.CategoryCode})")) : "Không có thế VIP (Số phổ thông / phụ)")}
- Phân tích đối chiếu thế nhịp khác: {features.AlternativeRhythmsComparison}
- Ghi chú phong thủy số học: {features.FengShuiNotes}

KẾT QUẢ TÍNH TOÁN GIÁ SÀN CƠ SỞ TOÁN HỌC:
- Giá sàn cơ sở (Baseline Math Price): {mathResult.BaselinePrice:N0} VNĐ
- Điểm uy tín đầu số (Prefix Reputation Factor): {mathResult.PrefixFactor:F2}
- Hệ số thế nhịp (Rhythm Multiplier Factor): {mathResult.RhythmFactor:F2}
- Tổng hệ số tổ hợp (Categories Cumulative Factor): {mathResult.CategoryFactor:F2}
- Hệ số phạt/thưởng phong thủy (FengShui Factor): {mathResult.FengShuiFactor:F2}

DỮ LIỆU THỊ TRƯỜNG THỰC TẾ & CẨM NANG PHONG THỦY (RAG CONTEXT):
{ragContextBuilder}

Dựa trên toàn bộ dữ liệu trên và tri thức thị trường SIM Việt Nam, hãy đưa ra kết luận định giá chính xác theo 3 kênh giao dịch và bài phân tích giải trình 4 phần dạng JSON.";

        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    { "temperature", 0.2f },
                    { "max_tokens", 800 }
                }
            };

            var rawText = await ExecuteChatWithCandidateModelsAsync(chatHistory, executionSettings, cts.Token);
            var cleanedJson = ExtractJsonString(rawText);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<AiValuationLlmResponse>(cleanedJson, options);

            if (parsed != null && parsed.SuggestedPrice > 0)
            {
                if (string.IsNullOrEmpty(parsed.FormattedNumber))
                {
                    parsed.FormattedNumber = features.FormattedNumber;
                }
                if (string.IsNullOrEmpty(parsed.RhythmRecommendation))
                {
                    parsed.RhythmRecommendation = features.SelectedRhythmValue;
                }
                if (parsed.TotalPoints <= 0)
                {
                    parsed.TotalPoints = features.TotalPoints;
                }

                parsed.EngineSource = "SEMANTIC_KERNEL_GEMINI";
                parsed.ExecutionTimeMs = sw.ElapsedMilliseconds;

                _logger.LogInformation("[GEMINI VALUATION SUCCESS] Thẩm định thành công số {Phone} trong {ElapsedMs}ms! Giá: {Price:N0} VNĐ, Nút: {Points}",
                    features.CleanPhoneNumber, sw.ElapsedMilliseconds, parsed.SuggestedPrice, parsed.TotalPoints);

                return parsed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GEMINI VALUATION FALLBACK] Lỗi khi gọi Gemini qua Semantic Kernel ({ElapsedMs}ms): {Msg}. Kích hoạt Fallback cục bộ.", sw.ElapsedMilliseconds, ex.Message);
        }

        var fallback = GenerateFallbackValuation(features, mathResult, effectiveChunks);
        fallback.EngineSource = "LOCAL_FALLBACK_ENGINE";
        fallback.ExecutionTimeMs = sw.ElapsedMilliseconds;
        return fallback;
    }

    public async Task<AiConsultationLlmResponse> GenerateConsultationWithAgenticToolsAsync(
        string userMessage,
        string conversationHistory,
        int? userBirthYear = null,
        string? userCareer = null,
        string? fengShuiElement = null,
        decimal? budgetMin = null,
        decimal? budgetMax = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng Agentic Fallback Dispatcher.");
            return await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);
        }

        try
        {
            var toolResults = await DispatchRelevantPluginsAsync(userMessage, userBirthYear, budgetMin, budgetMax, cancellationToken);
            var collectedSims = toolResults.SelectMany(t => t.SuggestedSims).ToList();

            var systemPrompt = BuildConsultationSystemPrompt(userBirthYear, userCareer, fengShuiElement, budgetMin, budgetMax);
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);

            foreach (var t in toolResults)
            {
                if (!string.IsNullOrWhiteSpace(t.ToolContextData))
                {
                    chatHistory.AddSystemMessage(t.ToolContextData);
                }
            }

            if (!string.IsNullOrWhiteSpace(conversationHistory))
            {
                chatHistory.AddUserMessage($"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:\n{conversationHistory}");
            }
            chatHistory.AddUserMessage(userMessage);

            var executionSettings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    { "temperature", 0.3f },
                    { "max_tokens", 1000 }
                }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(35));

            var reply = await ExecuteChatWithCandidateModelsAsync(chatHistory, executionSettings, cts.Token);

            if (!string.IsNullOrWhiteSpace(reply))
            {
                return new AiConsultationLlmResponse
                {
                    ReplyContent = reply,
                    SuggestedSims = collectedSims,
                    ReferencedChunkIds = new List<Guid>(),
                    ReferencedDocuments = new List<string>(),
                    TokensUsed = (int)(reply.Length / 3.5),
                    EngineSource = "SEMANTIC_KERNEL_GEMINI"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GEMINI CONSULTANT ERROR] Lỗi trong Semantic Kernel Agent loop: {Msg}", ex.Message);
        }

        return await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);
    }

    public async IAsyncEnumerable<ChatConsultationStreamEvent> StreamConsultationWithAgenticToolsAsync(
        string userMessage,
        string conversationHistory,
        int? userBirthYear = null,
        string? userCareer = null,
        string? fengShuiElement = null,
        decimal? budgetMin = null,
        decimal? budgetMax = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var collectedSims = new List<ConsultationSimSuggestion>();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng Stream Fallback Dispatcher.");
            await foreach (var evt in StreamFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken))
            {
                yield return evt;
            }
            yield break;
        }

        bool hasStreamedTokens = false;

        // 1. Phân loại & Kích hoạt các Native Plugins phù hợp theo nhu cầu thực tế của người dùng
        var toolResults = await DispatchRelevantPluginsAsync(userMessage, userBirthYear, budgetMin, budgetMax, cancellationToken);

        var systemPrompt = BuildConsultationSystemPrompt(userBirthYear, userCareer, fengShuiElement, budgetMin, budgetMax);
        var chatService = _kernel.GetRequiredService<IChatCompletionService>();

        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemPrompt);

        foreach (var t in toolResults)
        {
            yield return new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = t.ToolName,
                ToolSummary = $"Đang kích hoạt công cụ {t.ToolName}..."
            };

            if (t.SuggestedSims is { Count: > 0 })
            {
                collectedSims = t.SuggestedSims;
            }

            yield return new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = t.ToolName,
                ToolSummary = t.ToolSummary
            };

            if (!string.IsNullOrWhiteSpace(t.ToolContextData))
            {
                chatHistory.AddSystemMessage(t.ToolContextData);
            }
        }

        if (!string.IsNullOrWhiteSpace(conversationHistory))
        {
            chatHistory.AddUserMessage($"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:\n{conversationHistory}");
        }
        chatHistory.AddUserMessage(userMessage);

        var executionSettings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object>
            {
                { "temperature", 0.3f },
                { "max_tokens", 1000 }
            }
        };

        using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        streamCts.CancelAfter(TimeSpan.FromSeconds(35));

        string replyContent = string.Empty;
        try
        {
            replyContent = await ExecuteChatWithCandidateModelsAsync(chatHistory, executionSettings, streamCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GEMINI CONSULTANT ERROR] Lỗi khi nhận phản hồi từ Gemini: {Msg}", ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(replyContent))
        {
            hasStreamedTokens = true;
            var words = replyContent.Split(new[] { ' ' }, StringSplitOptions.None);
            for (int i = 0; i < words.Length; i++)
            {
                var word = words[i] + (i < words.Length - 1 ? " " : "");
                yield return new ChatConsultationStreamEvent
                {
                    EventType = "token",
                    Delta = word
                };
                await Task.Delay(12, cancellationToken);
            }
        }

        if (!hasStreamedTokens)
        {
            _logger.LogInformation("[GEMINI STREAM FALLBACK] Kích hoạt Stream Fallback sau {ElapsedMs}ms", sw.ElapsedMilliseconds);
            await foreach (var fallbackEvt in StreamFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken))
            {
                if (fallbackEvt.EventType == "done")
                {
                    if (collectedSims.Count == 0 && fallbackEvt.SuggestedSims != null)
                    {
                        collectedSims = fallbackEvt.SuggestedSims;
                    }
                }
                else
                {
                    yield return fallbackEvt;
                }
            }
        }

        // Final event
        yield return new ChatConsultationStreamEvent
        {
            EventType = "done",
            SuggestedSims = collectedSims,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = new List<string>(),
            EngineSource = hasStreamedTokens ? "SEMANTIC_KERNEL_GEMINI" : "LOCAL_FALLBACK_ENGINE",
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }

    private class DispatchedToolResult
    {
        public string ToolName { get; set; } = string.Empty;
        public string ToolSummary { get; set; } = string.Empty;
        public string ToolContextData { get; set; } = string.Empty;
        public List<ConsultationSimSuggestion> SuggestedSims { get; set; } = new();
    }

    private async Task<List<DispatchedToolResult>> DispatchRelevantPluginsAsync(
        string userMessage,
        int? userBirthYear,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var results = new List<DispatchedToolResult>();
        var lower = userMessage.ToLowerInvariant();

        using var scope = _scopeFactory.CreateScope();

        // 1. Auction Plugin (Đấu giá)
        if (lower.Contains("đấu giá") || lower.Contains("dau gia") || lower.Contains("phiên đấu") || lower.Contains("auction") || lower.Contains("bid"))
        {
            var auctionPlugin = scope.ServiceProvider.GetService<TrustSimBackend.Application.Ai.Plugins.AuctionPlugin>();
            if (auctionPlugin != null)
            {
                var res = await auctionPlugin.GetActiveAuctionsAsync(5, cancellationToken);
                results.Add(new DispatchedToolResult
                {
                    ToolName = "AuctionPlugin",
                    ToolSummary = "Đã tra cứu các phiên đấu giá SIM VIP trực tiếp trên sàn TrustSim",
                    ToolContextData = $"[DỮ LIỆU ĐẤU GIÁ TRỰC TIẾP TRÊN SÀN TRUSTSIM]:\n{res}\nHãy tư vấn chi tiết các phiên đấu giá trên cho khách hàng."
                });
            }
        }

        // 2. Feng Shui Knowledge Plugin (Phong thủy & Bản mệnh)
        if (lower.Contains("phong thủy") || lower.Contains("phong thuy") || lower.Contains("hợp mệnh") || lower.Contains("hop menh") ||
            lower.Contains("mệnh") || lower.Contains("menh") || lower.Contains("sinh năm") || lower.Contains("sinh nam") ||
            lower.Contains("ngũ hành") || lower.Contains("ngu hanh") || lower.Contains("tuổi") || lower.Contains("tuoi") ||
            lower.Contains("can chi") || userBirthYear.HasValue)
        {
            var fengShuiPlugin = scope.ServiceProvider.GetService<TrustSimBackend.Application.Ai.Plugins.FengShuiKnowledgePlugin>();
            if (fengShuiPlugin != null)
            {
                var sb = new StringBuilder();
                if (userBirthYear.HasValue)
                {
                    var profile = fengShuiPlugin.GetFengShuiGuidelineByBirthYear(userBirthYear.Value);
                    sb.AppendLine($"[HỒ SƠ PHONG THỦY BẢN MỆNH NĂM SINH {userBirthYear.Value}]:\n{profile}");
                }
                var knowledge = await fengShuiPlugin.QueryFengShuiKnowledgeAsync(userMessage, cancellationToken);
                sb.AppendLine($"[TRI THỨC PHONG THỦY RAG TỪ TRUSTSIM]:\n{knowledge}");

                results.Add(new DispatchedToolResult
                {
                    ToolName = "FengShuiKnowledgePlugin",
                    ToolSummary = "Đã truy xuất tri thức phong thủy ngũ hành & bản mệnh chuyên sâu",
                    ToolContextData = sb.ToString()
                });
            }
        }

        // 3. Sim Inventory Plugin (Kho SIM Marketplace)
        bool hasSearchIntent = lower.Contains("tìm sim") || lower.Contains("tim sim") || lower.Contains("mua sim") ||
                               lower.Contains("chọn sim") || lower.Contains("chon sim") || lower.Contains("gợi ý sim") ||
                               lower.Contains("goi y sim") || lower.Contains("danh sách sim") || lower.Contains("kho sim") ||
                               lower.Contains("có sim nào") || lower.Contains("co sim nao") || lower.Contains("bán sim") ||
                               lower.Contains("tầm giá") || lower.Contains("mức giá") ||
                               ((budgetMin.HasValue || budgetMax.HasValue) && (lower.Contains("sim") || lower.Contains("số")));

        if (hasSearchIntent)
        {
            var sims = await ExtractSimsFromInventoryAsync(budgetMin, budgetMax, cancellationToken);
            if (sims.Count > 0)
            {
                var sb = new StringBuilder("[KHO SIM THỰC TẾ TRÊN SÀN TRUSTSIM]:\n");
                foreach (var s in sims)
                {
                    sb.AppendLine($"- Số: {s.PhoneNumber} ({s.CarrierName}) | Giá: {s.Price:N0} đ | Đặc điểm: {s.HighlightReason}");
                }
                sb.AppendLine("LƯU Ý: Chỉ đề xuất các số SIM trên khi khách hàng có nhu cầu tìm mua hoặc xem số. Nếu khách chỉ hỏi kiến thức phong thủy hay hỏi mệnh của họ, hãy tập trung trả lời câu hỏi chính trước.");

                results.Add(new DispatchedToolResult
                {
                    ToolName = "SimInventoryPlugin",
                    ToolSummary = $"Tìm thấy {sims.Count} số SIM thực tế đang mở bán trên sàn",
                    ToolContextData = sb.ToString(),
                    SuggestedSims = sims
                });
            }
        }

        // 4. Trade & Escrow Plugin (Ký quỹ & An toàn giao dịch)
        if (lower.Contains("ký quỹ") || lower.Contains("ky quy") || lower.Contains("escrow") || lower.Contains("an toàn") ||
            lower.Contains("lừa đảo") || lower.Contains("phí sàn") || lower.Contains("đặt cọc") || lower.Contains("sang tên") ||
            lower.Contains("chính chủ"))
        {
            var escrowPlugin = scope.ServiceProvider.GetService<TrustSimBackend.Application.Ai.Plugins.TradeAndEscrowPlugin>();
            if (escrowPlugin != null)
            {
                var policy = escrowPlugin.GetEscrowAndSafetyPolicy();
                results.Add(new DispatchedToolResult
                {
                    ToolName = "TradeAndEscrowPlugin",
                    ToolSummary = "Đã tra cứu quy trình giao dịch đảm bảo TrustSim Escrow & chính sách an toàn",
                    ToolContextData = $"[QUY TRÌNH KÝ QUỸ ESCROW & BẢO VỆ GIAO DỊCH]:\n{policy}"
                });
            }
        }

        // 5. User Wallet Plugin (Ví tiền & SePay)
        if (lower.Contains("số dư") || lower.Contains("so du") || lower.Contains("ví") || lower.Contains("nạp tiền") ||
            lower.Contains("sepay") || lower.Contains("vietqr") || lower.Contains("chuyển khoản"))
        {
            var walletPlugin = scope.ServiceProvider.GetService<TrustSimBackend.Application.Ai.Plugins.UserWalletPlugin>();
            if (walletPlugin != null)
            {
                var bal = await walletPlugin.GetMyWalletBalanceAsync(cancellationToken);
                results.Add(new DispatchedToolResult
                {
                    ToolName = "UserWalletPlugin",
                    ToolSummary = "Đã kiểm tra số dư ví & hướng dẫn nạp SePay VietQR",
                    ToolContextData = $"[THÔNG TIN VÍ & NẠP TIỀN SEPAY VIETQR]:\n{bal}"
                });
            }
        }

        // 6. Sim Cadence Plugin (Bóc tách thế nhịp số cụ thể)
        var phoneMatch = Regex.Match(userMessage, @"0\d{9}");
        if (phoneMatch.Success)
        {
            var cadencePlugin = scope.ServiceProvider.GetService<TrustSimBackend.Application.Ai.Plugins.SimCadencePlugin>();
            if (cadencePlugin != null)
            {
                var cadence = cadencePlugin.AnalyzeSimCadence(phoneMatch.Value);
                results.Add(new DispatchedToolResult
                {
                    ToolName = "SimCadencePlugin",
                    ToolSummary = $"Đã bóc tách thế nhịp và phân tích âm luật số {phoneMatch.Value}",
                    ToolContextData = $"[BÓC TÁCH THẾ NHỊP SỐ {phoneMatch.Value}]:\n{cadence}"
                });
            }
        }

        return results;
    }

    private string BuildConsultationSystemPrompt(
        int? userBirthYear,
        string? userCareer,
        string? fengShuiElement,
        decimal? budgetMin,
        decimal? budgetMax)
    {
        return $@"Bạn là Chuyên gia Cao cấp Tư vấn SIM Số đẹp & Phong thủy tại Sàn Giao dịch TrustSim Việt Nam.
Bạn có quyền truy cập vào các công cụ trong hệ sinh thái sàn TrustSim (Tra cứu Kho SIM, Tri thức phong thủy RAG, Bóc tách thế nhịp, Thông tin đấu giá trực tiếp, Ký quỹ Escrow và Số dư ví).

NGUYÊN TẮC TƯ VẤN:
- Chào hỏi niềm nở, phong thái chuyên gia viễn thông & phong thủy số học uy tín.
- Khi khách hỏi tìm SIM hoặc hỏi còn số nào không, ưu tiên sử dụng danh sách SIM thực tế từ kho TrustSim.
- Giải thích cặn kẽ ý nghĩa phong thủy theo bản mệnh ngũ hành và ngành nghề của khách hàng.
- Thông tin khách hàng: Năm sinh: {userBirthYear?.ToString() ?? "Chưa rõ"}, Nghề nghiệp: {userCareer ?? "Chưa rõ"}, Mệnh: {fengShuiElement ?? "Chưa rõ"}, Ngân sách: {(budgetMin.HasValue ? budgetMin.Value.ToString("N0") : "0")} - {(budgetMax.HasValue ? budgetMax.Value.ToString("N0") : "Không giới hạn")} VND.";
    }

    private async Task<List<ConsultationSimSuggestion>> ExtractSimsFromInventoryAsync(
        decimal? minPrice,
        decimal? maxPrice,
        CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var simRepo = scope.ServiceProvider.GetService<ISimNumberRepository>();
            if (simRepo != null)
            {
                var sims = await simRepo.GetAvailableSimsForConsultationAsync(minPrice, maxPrice, null, null, null, limit: 6, cancellationToken);
                return sims.Select(s => new ConsultationSimSuggestion
                {
                    SimNumberId = s.Id,
                    PhoneNumber = s.PhoneNumber,
                    Price = s.Price,
                    CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                    HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi lấy danh sách SIM từ kho: {Msg}", ex.Message);
        }

        return new List<ConsultationSimSuggestion>();
    }

    private AiValuationLlmResponse GenerateFallbackValuation(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult>? ragChunks)
    {
        decimal baseP = mathResult.BaselinePrice;
        decimal minP = Math.Round(baseP * 0.88m / 100000) * 100000;
        decimal maxP = Math.Round(baseP * 1.25m / 100000) * 100000;
        decimal suggestedP = Math.Round(baseP / 100000) * 100000;

        var categoriesStr = features.DetectedCategories.Count > 0
            ? string.Join(", ", features.DetectedCategories.Select(c => c.CategoryName))
            : "Số đẹp phổ thông";

        var reasoning = new StringBuilder();
        reasoning.AppendLine($"Số SIM {features.FormattedNumber} được bóc tách định dạng theo nhịp {features.SelectedRhythmValue} ({features.DetectedRhythmName}), bộc lộ các thế số giá trị: {categoriesStr}.");
        reasoning.AppendLine($"Âm luật phát âm nhịp nhàng, tạo ấn tượng mạnh và gia tăng tính nhận diện thương hiệu cho chủ sở hữu.");
        reasoning.AppendLine($"Hệ số uy tín đầu số {features.Prefix} của nhà mạng {features.CarrierName} kết hợp với thế nhịp ngắt tối ưu nâng tầm giá trị cơ sở đạt {suggestedP:N0} VNĐ.");
        reasoning.AppendLine($"Khảo sát thị trường viễn thông thực tế, mức giá giao dịch đề xuất là {suggestedP:N0} VNĐ (Khoảng giá hợp lý: {minP:N0} - {maxP:N0} VNĐ).");

        return new AiValuationLlmResponse
        {
            SuggestedPrice = suggestedP,
            MinPrice = minP,
            MaxPrice = maxP,
            TotalPoints = features.TotalPoints,
            ConfidenceScore = 0.95f,
            MarketMultiplier = 1.0f,
            RhythmRecommendation = features.SelectedRhythmValue,
            FormattedNumber = features.FormattedNumber,
            CategoryCodes = features.DetectedCategories.Select(c => c.CategoryCode).ToList(),
            ValuationReasoning = reasoning.ToString(),
            FengShuiSummary = features.FengShuiNotes,
            EngineSource = "LOCAL_FALLBACK_ENGINE"
        };
    }

    private async Task<AiConsultationLlmResponse> ExecuteFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var sims = await ExtractSimsFromInventoryAsync(budgetMin, budgetMax, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        sb.AppendLine("Dựa trên thông tin và tiêu chí tìm kiếm của bạn, tôi xin gợi ý các định hướng chọn số mang lại vượng khí tốt nhất:");
        sb.AppendLine("- Nên ưu tiên các đầu số cổ uy tín của các nhà mạng lớn (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) để khẳng định vị thế và uy tín cá nhân.");
        sb.AppendLine("- Kết hợp các đuôi số phong thủy kích hoạt tài lộc như Thần tài (39, 79), Lộc phát (68, 86) hoặc số tiến (6789, 1368).");

        if (sims.Count > 0)
        {
            sb.AppendLine("\nTôi đã rà soát kho SIM thực tế trên sàn TrustSim và tuyển chọn các số đẹp phù hợp nhất dưới đây để bạn tham khảo:");
            foreach (var sim in sims)
            {
                sb.AppendLine($"• {sim.PhoneNumber} ({sim.CarrierName}) - Giá: {sim.Price:N0} đ : {sim.HighlightReason}");
            }
        }
        else
        {
            sb.AppendLine("\nHiện tại trong kho đang cập nhật thêm các số mới trong phân khúc này. Bạn có thể mở rộng khoảng giá hoặc liên hệ Hotline sàn để được hỗ trợ chuyên sâu.");
        }

        return new AiConsultationLlmResponse
        {
            ReplyContent = sb.ToString(),
            SuggestedSims = sims,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = new List<string>(),
            TokensUsed = 120
        };
    }

    private async IAsyncEnumerable<ChatConsultationStreamEvent> StreamFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var fallbackRes = await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);

        if (fallbackRes.SuggestedSims.Count > 0)
        {
            yield return new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimInventory",
                ToolSummary = "Đang quét kho SIM trên sàn TrustSim..."
            };
            await Task.Delay(40, cancellationToken);
            yield return new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimInventory",
                ToolSummary = $"Tìm thấy {fallbackRes.SuggestedSims.Count} số SIM phù hợp"
            };
        }

        var words = fallbackRes.ReplyContent.Split(new[] { ' ' }, StringSplitOptions.None);
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i] + (i < words.Length - 1 ? " " : "");
            yield return new ChatConsultationStreamEvent
            {
                EventType = "token",
                Delta = word
            };
            await Task.Delay(15, cancellationToken);
        }

        yield return new ChatConsultationStreamEvent
        {
            EventType = "done",
            SuggestedSims = fallbackRes.SuggestedSims,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = new List<string>(),
            EngineSource = "LOCAL_FALLBACK_ENGINE"
        };
    }

    private static string ExtractJsonString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        var match = Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
        }

        return text.Trim();
    }

    private async Task<string> ExecuteChatWithCandidateModelsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings executionSettings,
        CancellationToken cancellationToken)
    {
        // 1. Thử gọi qua Semantic Kernel trước
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var result = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, _kernel, cancellationToken);
            if (!string.IsNullOrWhiteSpace(result?.Content))
            {
                return CleanLlmResponse(result.Content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[SK CHAT RETRY] Semantic Kernel call failed ({Ex}): Đang thử fallback trực tiếp qua REST API...", ex.Message);
        }

        // 2. Danh sách candidate models tối ưu theo tốc độ và độ ổn định thực tế
        var configuredModel = !string.IsNullOrWhiteSpace(_settings.ChatModel) ? _settings.ChatModel : "gemini-3.1-flash-lite";
        var candidateModels = new HashSet<string>
        {
            configuredModel,
            "gemini-3.1-flash-lite",
            "gemma-4-26b-a4b-it",
            "gemini-3.6-flash"
        };

        foreach (var model in candidateModels)
        {
            try
            {
                var content = await CallDirectOpenAiCompatAsync(model, chatHistory, cancellationToken);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    _logger.LogInformation("[LLM DIRECT SUCCESS] Model {Model} phản hồi thành công!", model);
                    return CleanLlmResponse(content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[LLM CANDIDATE FAILED] Model {Model} thất bại: {Msg}", model, ex.Message);
            }
        }

        return string.Empty;
    }

    private async Task<string> CallDirectOpenAiCompatAsync(
        string model,
        ChatHistory chatHistory,
        CancellationToken cancellationToken)
    {
        var messages = chatHistory.Select(m => new
        {
            role = m.Role == AuthorRole.System ? "system" : m.Role == AuthorRole.Assistant ? "assistant" : "user",
            content = m.Content ?? string.Empty
        }).ToList();

        var payload = new
        {
            model = model,
            messages = messages,
            temperature = 0.25,
            max_tokens = 1000
        };

        var json = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(20));

        var response = await _httpClient.SendAsync(request, cts.Token);
        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[DIRECT REST ERROR] Model {Model} status {Status}: {Body}", model, response.StatusCode, responseBody);
            return string.Empty;
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var message = choices[0].GetProperty("message");
            if (message.TryGetProperty("content", out var contentElem))
            {
                return contentElem.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string CleanLlmResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var cleaned = Regex.Replace(raw, @"<thought>[\s\S]*?</thought>", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
    }
}
