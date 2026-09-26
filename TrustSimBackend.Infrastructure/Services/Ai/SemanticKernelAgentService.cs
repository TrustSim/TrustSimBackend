using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using TrustSimBackend.Application.Ai.Plugins;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Dịch vụ Core AI Agent sử dụng Google Gemini API với Native Function Calling và Semantic Kernel.
/// Tự động trích xuất tham số tra cứu kho SIM, tri thức phong thủy RAG, đấu giá VIP, và định giá 5 phân khúc.
/// </summary>
public class SemanticKernelAgentService : ISemanticKernelAgentService
{
    private readonly Kernel _kernel;
    private readonly GeminiSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SemanticKernelAgentService> _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

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

    #region 1. SIM Valuation (Định giá SIM Đa Tầng)

    public async Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // 1. Tự động kiểm tra nếu ragChunks rỗng, AI Agent tự lấy RAG chunks từ Repository
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
Nhiệm vụ: Thẩm định giá trị thực tế của số SIM dựa trên dữ liệu giao dịch và biểu giá niêm yết từ các sàn giao dịch SIM lớn nhất Việt Nam (như Sim Thăng Long simthanglong.vn, Chợ Sim 24h chosim24h.com, Sim.vn, Tổng Kho Sim).

QUY TẮC ĐỊNH GIÁ QUAN TRỌNG:
1. LUÔN TIN TƯỞNG VÀ ƯU TIÊN TRI THỨC THỊ TRƯỜNG THỰC TẾ & DỮ LIỆU TỪ CÁC SÀN LỚN BÊN NGOÀI INTERNET. Không để công thức toán học cơ sở tuyến tính làm kéo tụt giá trị của các dòng SIM Siêu VIP.
2. PHÂN TÁCH RÕ RÀNG 3 KÊNH GIAO DỊCH:
   - Kênh 1: Giá Niêm Yết Showroom / Đại Lý Lớn (Retail Price - MaxPrice): Giá bán lẻ chính thức tại các showroom lớn như Sim Thăng Long (có cam kết pháp lý, hỗ trợ bảo hành trọn đời và trả góp).
   - Kênh 2: Giá Thị Trường Tự Do P2P (Market P2P Price - SuggestedPrice): Giá chuyển nhượng trực tiếp giữa người dùng hoặc bán thẳng cho Doanh nhân / Chủ tịch (thường thấp hơn giá Showroom 15% - 25%).
   - Kênh 3: Giá Thương Lái / Thợ SIM Thu Mua Nhanh (Dealer Buyback Price - MinPrice): Giá thợ hoặc thương lái sẵn sàng chi tiền mặt thu mua trong 24h (chiết khấu an toàn 25% - 35% so với giá P2P).

3. MA TRẬN PHÂN CẤP THỊ TRƯỜNG THỰC TẾ (5 PHÂN KHÚC):
   - Tầng 1 (Phổ thông / Số phụ): Giá 150.000đ – 800.000đ. Áp dụng cho số thuê bao thông thường, có cặp lặp đơn giản (như 77), thế số phụ không có tổ hợp VIP (ví dụ 086.919.2776 giá P2P ~350k - 500k, đại lý ~600k, thợ thu ~250k). TUYỆT ĐỐI không định giá tiền triệu cho số phổ thông.
   - Tầng 2 (Số đẹp Tầm Trung): Giá 1.000.000đ – 50.000.000đ. Áp dụng cho Tam hoa (0-5: 1tr-20tr, 6/8/9: 20tr-50tr), Thần tài (39, 79), Lộc phát (68, 86), Cặp lặp kép, Gánh đảo đẹp, Taxi 2 cặp.
   - Tầng 3 (Hotline Doanh nhân / VIP): Giá 50.000.000đ – 450.000.000đ. Áp dụng cho Tứ quý (1, 2, 3, 4, 7: 50tr-200tr; 6, 8, 9 đầu mới: 150tr-400tr), Sảnh tiến 6789 (150tr-400tr đầu cổ), Taxi 3 cặp, Lộc phát VIP (6868, 8686).
   - Tầng 4 (Siêu VIP):
     + Tứ quý 6666, 8888, 9999 ĐẦU CỔ 098/090/091: 400 triệu – 900 triệu VNĐ.
     + Ngũ quý 0, 1, 2, 3, 4, 5, 7 đầu mới: 150 triệu – 500 triệu VNĐ.
     + Ngũ quý 2, 3, 5, 7 đầu cổ: 300 triệu – 700 triệu VNĐ.
     + **NGŨ QUÝ 66666, 88888, 99999 ĐẦU CỔ 098, 090, 091 (Ví dụ 098.56.88888 - Ngũ quý 8 Viettel 098)**:
       * Giá Niêm Yết Showroom/Đại Lý Lớn (Sim Thăng Long, Chợ Sim 24h): **1,8 tỷ – 2,3 tỷ VNĐ** (Tham chiếu niêm yết simthanglong.vn: ~1.888.000.000 ₫).
       * Giá Chợ Tự Do P2P (Bán thẳng cho Doanh nhân/Chủ tịch): **1,3 tỷ – 1,7 tỷ VNĐ** (Đề xuất: ~1,5 tỷ VNĐ).
       * Giá Thợ Thu Mua 24h Tiền Mặt: **900 triệu – 1,2 tỷ VNĐ**.
     + Lục quý (666666, 888888, 999999): 4 tỷ – 15 tỷ VNĐ.
     + Sảnh rồng 6 số (456789, 123456) đầu cổ: 800 triệu – 2,5 tỷ VNĐ.
     + Đại Taxi 4 Cặp (86.86.86.86, 68.68.68.68): 1,5 tỷ – 6 tỷ VNĐ.
   - Tầng 5 (Di sản / Đấu giá Cấp cao): Hàng chục tỷ VNĐ. Bát quý (88888888, 99999999), Cửu quý đầu cổ 098/090/091 độc nhất vô nhị: 15 tỷ – 40 tỷ VNĐ.

Hãy trả về kết quả thuần JSON tuân thủ cấu trúc sau (không kèm markdown ```json bọc ngoài):
{
  ""suggestedPrice"": 1500000000,
  ""minPrice"": 1050000000,
  ""maxPrice"": 1888000000,
  ""totalPoints"": 5,
  ""confidenceScore"": 0.98,
  ""marketMultiplier"": 1.0,
  ""rhythmRecommendation"": ""3.2.5"",
  ""formattedNumber"": ""098.56.88888"",
  ""categoryCodes"": [""NGU_QUY_8"", ""DAU_SO_CO_098""],
  ""valuationReasoning"": ""1. Phân tích cấu trúc & thế số:\n- Đầu số 098: Thuộc nhà mạng Viettel đầu cổ danh giá...\n- Thân giữa 56: Thế số Sinh Lộc...\n- Cụm đuôi 88888: Ngũ quý 8 (Ngũ Đại Phát) Siêu VIP...\n\n2. Tổng nút (Điểm số) & Phong thủy:\n- Tổng nút: ...\n\n3. Phân cấp phân khúc thị trường:\n- Thuộc phân khúc SIM Siêu VIP cực phẩm...\n\n4. Bảng định giá 3 kênh giao dịch đối chiếu sàn Sim Thăng Long / Chợ Sim 24h:\n- Giá mua mới đại lý / Showroom (Sim Thăng Long): 1.888.000.000 đ\n- Giá sang tay tự do P2P: 1.500.000.000 đ\n- Giá thợ thu mua thanh lý nhanh 24h: 1.050.000.000 đ"",
  ""fengShuiSummary"": ""Tóm tắt ngắn gọn ý nghĩa phong thủy và đẳng cấp nhận diện thương hiệu...""
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
            var chatHistory = new List<OpenAiMessage>
            {
                new() { Role = "system", Content = systemPrompt },
                new() { Role = "user", Content = userPrompt }
            };

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var rawText = await ExecuteChatWithCandidateModelsAsync(chatHistory, includeTools: false, maxTokens: 2048, cancellationToken: cts.Token);
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
            _logger.LogWarning(ex, "[GEMINI VALUATION FALLBACK] Lỗi khi gọi Gemini ({ElapsedMs}ms): {Msg}. Kích hoạt Fallback cục bộ.", sw.ElapsedMilliseconds, ex.Message);
        }

        var fallback = GenerateFallbackValuation(features, mathResult, effectiveChunks);
        fallback.EngineSource = "LOCAL_FALLBACK_ENGINE";
        fallback.ExecutionTimeMs = sw.ElapsedMilliseconds;
        return fallback;
    }

    #endregion

    #region 2. AI Consultant Agent (Tư vấn SIM & Native Function Calling)

    private class AgentLoopResult
    {
        public List<ChatConsultationStreamEvent> ToolEvents { get; set; } = new();
        public List<ConsultationSimSuggestion> CollectedSims { get; set; } = new();
        public List<string> ReferencedDocs { get; set; } = new();
        public string ReplyContent { get; set; } = string.Empty;
    }

    private async Task<AgentLoopResult> ProcessConsultationAgentLoopAsync(
        string userMessage,
        string conversationHistory,
        int? userBirthYear,
        string? userCareer,
        string? fengShuiElement,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var result = new AgentLoopResult();
        var systemPrompt = BuildConsultationSystemPrompt(userBirthYear, userCareer, fengShuiElement, budgetMin, budgetMax);
        var messages = new List<OpenAiMessage>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        if (!string.IsNullOrWhiteSpace(conversationHistory))
        {
            messages.Add(new OpenAiMessage { Role = "user", Content = $"[LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ]:\n{conversationHistory}" });
            messages.Add(new OpenAiMessage { Role = "assistant", Content = "Tôi đã nắm rõ toàn bộ lịch sử tư vấn và sở thích của bạn." });
        }

        messages.Add(new OpenAiMessage { Role = "user", Content = userMessage });

        // Vòng lặp Agentic Tool Resolution (Tối đa 3 lượt gọi tool liên hoàn)
        const int maxToolTurns = 3;
        for (int turn = 0; turn < maxToolTurns; turn++)
        {
            var (choice, usedModel) = await ExecuteLlmWithFailoverAsync(
                messages,
                includeTools: true,
                maxTokens: 1500,
                temperature: 0.2f,
                cancellationToken: cancellationToken);

            if (choice.ToolCalls == null || choice.ToolCalls.Count == 0)
            {
                result.ReplyContent = CleanLlmResponse(choice.Content ?? string.Empty);
                break;
            }

            messages.Add(choice);

            foreach (var tc in choice.ToolCalls)
            {
                _logger.LogInformation("[NATIVE TOOL CALL] Model {Model} -> Function: {Func}, Args: {Args}", usedModel, tc.Function.Name, tc.Function.Arguments);

                result.ToolEvents.Add(new ChatConsultationStreamEvent
                {
                    EventType = "tool_start",
                    ToolName = tc.Function.Name,
                    ToolSummary = $"Đang kích hoạt công cụ {tc.Function.Name}..."
                });

                using var argsDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(tc.Function.Arguments) ? "{}" : tc.Function.Arguments);
                var (resultJson, summary, foundSims) = await ExecuteNativeToolAsync(tc.Function.Name, argsDoc.RootElement, cancellationToken);

                if (foundSims.Count > 0)
                {
                    result.CollectedSims.AddRange(foundSims);
                }
                if (tc.Function.Name == "QueryFengShuiKnowledge")
                {
                    result.ReferencedDocs.Add("Cẩm nang Phong Thủy & Số Học TrustSim");
                }

                result.ToolEvents.Add(new ChatConsultationStreamEvent
                {
                    EventType = "tool_end",
                    ToolName = tc.Function.Name,
                    ToolSummary = summary
                });

                messages.Add(new OpenAiMessage
                {
                    Role = "tool",
                    ToolCallId = tc.Id,
                    Content = resultJson
                });
            }
        }

        // Nếu sau các lượt gọi tool mà chưa có nội dung trả lời cuối cùng, thực hiện lượt tổng hợp không kèm tools
        if (string.IsNullOrWhiteSpace(result.ReplyContent))
        {
            var (finalChoice, _) = await ExecuteLlmWithFailoverAsync(
                messages,
                includeTools: false,
                maxTokens: 1500,
                temperature: 0.3f,
                cancellationToken: cancellationToken);

            result.ReplyContent = CleanLlmResponse(finalChoice.Content ?? string.Empty);
        }

        return result;
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
        if (!IsGeminiApiKeyConfigured())
        {
            _logger.LogInformation("Gemini API Key chưa cấu hình hoặc không hợp lệ (cần bắt đầu bằng AIzaSy). Sử dụng Smart Local Agent Engine.");
            return await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);
        }

        try
        {
            var loopRes = await ProcessConsultationAgentLoopAsync(
                userMessage,
                conversationHistory,
                userBirthYear,
                userCareer,
                fengShuiElement,
                budgetMin,
                budgetMax,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(loopRes.ReplyContent))
            {
                return new AiConsultationLlmResponse
                {
                    ReplyContent = loopRes.ReplyContent,
                    SuggestedSims = loopRes.CollectedSims,
                    ReferencedChunkIds = new List<Guid>(),
                    ReferencedDocuments = loopRes.ReferencedDocs.Distinct().ToList(),
                    TokensUsed = (int)(loopRes.ReplyContent.Length / 3.5),
                    EngineSource = "SEMANTIC_KERNEL_GEMINI"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GEMINI CONSULTANT ERROR] Lỗi trong Native Function Calling Loop: {Msg}", ex.Message);
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
        var referencedDocs = new List<string>();

        if (!IsGeminiApiKeyConfigured())
        {
            _logger.LogInformation("Gemini API Key chưa cấu hình hoặc không hợp lệ. Sử dụng Smart Local Agent Streaming Engine.");
            await foreach (var evt in StreamFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken))
            {
                yield return evt;
            }
            yield break;
        }

        bool hasStreamedTokens = false;
        string replyContent = string.Empty;
        AgentLoopResult? loopRes = null;

        try
        {
            loopRes = await ProcessConsultationAgentLoopAsync(
                userMessage,
                conversationHistory,
                userBirthYear,
                userCareer,
                fengShuiElement,
                budgetMin,
                budgetMax,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GEMINI STREAM CONSULTANT ERROR] Lỗi khi xử lý Stream Agent loop: {Msg}", ex.Message);
        }

        if (loopRes != null)
        {
            foreach (var toolEvt in loopRes.ToolEvents)
            {
                yield return toolEvt;
                await Task.Delay(15, cancellationToken);
            }

            collectedSims = loopRes.CollectedSims;
            referencedDocs = loopRes.ReferencedDocs;
            replyContent = loopRes.ReplyContent;
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
                await Task.Delay(10, cancellationToken);
            }
        }

        if (!hasStreamedTokens)
        {
            _logger.LogInformation("[GEMINI STREAM FALLBACK] Kích hoạt Smart Local Stream sau {ElapsedMs}ms", sw.ElapsedMilliseconds);
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
            ReferencedDocuments = referencedDocs.Distinct().ToList(),
            EngineSource = hasStreamedTokens ? "SEMANTIC_KERNEL_GEMINI" : "LOCAL_FALLBACK_ENGINE",
            ExecutionTimeMs = sw.ElapsedMilliseconds
        };
    }


    #endregion

    #region 3. Native Function Execution Dispatcher

    private async Task<(string ResultJson, string Summary, List<ConsultationSimSuggestion> FoundSims)> ExecuteNativeToolAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        switch (toolName)
        {
            case "SearchAvailableSims":
            {
                var simRepo = sp.GetRequiredService<ISimNumberRepository>();
                decimal? minPrice = arguments.TryGetProperty("minPrice", out var minP) && minP.ValueKind == JsonValueKind.Number ? minP.GetDecimal() : null;
                decimal? maxPrice = arguments.TryGetProperty("maxPrice", out var maxP) && maxP.ValueKind == JsonValueKind.Number ? maxP.GetDecimal() : null;
                string? carrier = arguments.TryGetProperty("carrier", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                string? prefix = arguments.TryGetProperty("prefix", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                string? keyword = arguments.TryGetProperty("keyword", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;

                var sims = await simRepo.GetAvailableSimsForConsultationAsync(minPrice, maxPrice, carrier, prefix, keyword, limit: 6, cancellationToken);
                var suggestions = sims.Select(s => new ConsultationSimSuggestion
                {
                    SimNumberId = s.Id,
                    PhoneNumber = s.PhoneNumber,
                    Price = s.Price,
                    CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                    HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
                }).ToList();

                var resultJson = JsonSerializer.Serialize(new
                {
                    total = sims.Count,
                    items = suggestions.Select(s => new
                    {
                        simNumberId = s.SimNumberId,
                        phoneNumber = s.PhoneNumber,
                        price = s.Price,
                        carrier = s.CarrierName,
                        highlight = s.HighlightReason
                    })
                });

                var summary = suggestions.Count > 0
                    ? $"Tìm thấy {suggestions.Count} số SIM thực tế đang mở bán trên sàn TrustSim"
                    : "Không tìm thấy số SIM phù hợp với tiêu chí trong kho hiện tại";

                return (resultJson, summary, suggestions);
            }

            case "GetSimDetails":
            {
                var simRepo = sp.GetRequiredService<ISimNumberRepository>();
                string phone = arguments.TryGetProperty("phoneNumber", out var p) ? p.GetString() ?? "" : "";
                var sim = await simRepo.GetByPhoneNumberAsync(phone, cancellationToken);
                if (sim == null)
                {
                    return ("{\"error\":\"Không tìm thấy số SIM này trong hệ thống.\"}", $"Không tìm thấy số {phone}", new List<ConsultationSimSuggestion>());
                }

                var sugg = new ConsultationSimSuggestion
                {
                    SimNumberId = sim.Id,
                    PhoneNumber = sim.PhoneNumber,
                    Price = sim.Price,
                    CarrierName = sim.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                    HighlightReason = $"{sim.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {sim.PhonePrefix?.PrefixValue}"
                };

                var json = JsonSerializer.Serialize(new
                {
                    phoneNumber = sim.PhoneNumber,
                    price = sim.Price,
                    status = sim.SimStatus.ToString(),
                    carrier = sugg.CarrierName,
                    rhythm = sim.FormatRhythm?.FormatRhythmName
                });

                return (json, $"Tra cứu chi tiết số {phone}", new List<ConsultationSimSuggestion> { sugg });
            }

            case "QueryFengShuiKnowledge":
            {
                var plugin = sp.GetRequiredService<FengShuiKnowledgePlugin>();
                string query = arguments.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                var res = await plugin.QueryFengShuiKnowledgeAsync(query, cancellationToken);
                return (res, "Đã tra cứu tri thức phong thủy ngũ hành từ cẩm nang TrustSim", new List<ConsultationSimSuggestion>());
            }

            case "GetFengShuiGuidelineByBirthYear":
            {
                var plugin = sp.GetRequiredService<FengShuiKnowledgePlugin>();
                int year = arguments.TryGetProperty("birthYear", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : 2000;
                var res = plugin.GetFengShuiGuidelineByBirthYear(year);
                return (res, $"Đã xác định bản mệnh ngũ hành & số cát tường năm sinh {year}", new List<ConsultationSimSuggestion>());
            }

            case "GetActiveAuctions":
            {
                var plugin = sp.GetRequiredService<AuctionPlugin>();
                string? status = arguments.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String ? st.GetString() : null;
                var res = await plugin.GetActiveAuctionsAsync(status, cancellationToken);
                
                var auctionSims = new List<ConsultationSimSuggestion>();
                try
                {
                    using var aDoc = JsonDocument.Parse(res);
                    if (aDoc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var it in items.EnumerateArray())
                        {
                            if (it.TryGetProperty("simNumberId", out var sidProp) && sidProp.TryGetGuid(out var sid))
                            {
                                var aCode = it.TryGetProperty("auctionCode", out var ac) ? ac.GetString() ?? "" : "";
                                var aStatus = it.TryGetProperty("status", out var stp) ? stp.GetString() ?? "" : "";
                                auctionSims.Add(new ConsultationSimSuggestion
                                {
                                    SimNumberId = sid,
                                    PhoneNumber = it.TryGetProperty("phoneNumber", out var pn) ? pn.GetString() ?? "" : "",
                                    Price = it.TryGetProperty("currentPrice", out var cp) ? cp.GetDecimal() : 0,
                                    CarrierName = it.TryGetProperty("carrier", out var cr) ? cr.GetString() ?? "Viễn thông" : "Viễn thông",
                                    HighlightReason = $"Đấu giá {aCode} ({aStatus})"
                                });
                            }
                        }
                    }
                }
                catch { }

                return (res, "Đã tra cứu danh sách phiên đấu giá SIM VIP trên sàn TrustSim", auctionSims);
            }

            case "GetEscrowAndSafetyPolicy":
            {
                var plugin = sp.GetRequiredService<TradeAndEscrowPlugin>();
                var res = plugin.GetEscrowAndSafetyPolicy();
                return (res, "Đã tra cứu chính sách giao dịch đảm bảo TrustSim Escrow", new List<ConsultationSimSuggestion>());
            }

            case "CalculateTradeFees":
            {
                var plugin = sp.GetRequiredService<TradeAndEscrowPlugin>();
                decimal price = arguments.TryGetProperty("price", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetDecimal() : 0;
                var res = plugin.CalculateTradeFees(price);
                return (res, $"Đã tính toán biểu phí sàn & tiền đặt cọc cho mức giá {price:N0} đ", new List<ConsultationSimSuggestion>());
            }

            case "GetMyWalletBalance":
            {
                var plugin = sp.GetRequiredService<UserWalletPlugin>();
                var res = await plugin.GetMyWalletBalanceAsync(cancellationToken);
                return (res, "Đã kiểm tra số dư ví khả dụng của tài khoản", new List<ConsultationSimSuggestion>());
            }

            case "GetSePayDepositGuideline":
            {
                var plugin = sp.GetRequiredService<UserWalletPlugin>();
                decimal amount = arguments.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : 100000;
                var res = plugin.GetSePayDepositGuideline(amount);
                return (res, "Đã lấy thông tin chuyển khoản nạp tiền tự động SePay VietQR", new List<ConsultationSimSuggestion>());
            }

            case "AnalyzeSimCadence":
            {
                var plugin = sp.GetRequiredService<SimCadencePlugin>();
                string phone = arguments.TryGetProperty("phoneNumber", out var p) ? p.GetString() ?? "" : "";
                var res = plugin.AnalyzeSimCadence(phone);
                return (res, $"Đã bóc tách thế nhịp và âm luật số {phone}", new List<ConsultationSimSuggestion>());
            }

            case "EvaluateSimPrice":
            {
                var plugin = sp.GetRequiredService<SimValuationPlugin>();
                string phone = arguments.TryGetProperty("phoneNumber", out var p) ? p.GetString() ?? "" : "";
                var res = plugin.EvaluateSimPrice(phone);
                return (res, $"Đã thẩm định cấu trúc giá trị và phân khúc thị trường số {phone}", new List<ConsultationSimSuggestion>());
            }

            case "GetMarketPriceComparables":
            {
                var plugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
                string? cat = arguments.TryGetProperty("categoryCode", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                string? pref = arguments.TryGetProperty("prefix", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
                var res = await plugin.GetMarketPriceComparablesAsync(cat, pref, cancellationToken);
                return (res, $"Đã tra cứu biểu giá tham chiếu thị trường cho thể loại {cat ?? "chung"}", new List<ConsultationSimSuggestion>());
            }

            case "GetSimCategories":
            {
                var plugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
                var res = await plugin.GetSimCategoriesAsync(cancellationToken);
                return (res, "Đã tra cứu danh mục thể loại số đẹp trên sàn TrustSim", new List<ConsultationSimSuggestion>());
            }

            case "GetCarrierInfo":
            {
                var plugin = sp.GetRequiredService<CarrierPolicyPlugin>();
                string carrier = arguments.TryGetProperty("carrierName", out var c) ? c.GetString() ?? "" : "";
                var res = await plugin.GetCarrierInfoAsync(carrier, cancellationToken);
                return (res, $"Đã tra cứu thông tin nhà mạng & thủ tục sang tên {carrier}", new List<ConsultationSimSuggestion>());
            }

            case "GetPrefixInfo":
            {
                var plugin = sp.GetRequiredService<CarrierPolicyPlugin>();
                string prefix = arguments.TryGetProperty("prefixValue", out var p) ? p.GetString() ?? "" : "";
                var res = await plugin.GetPrefixInfoAsync(prefix, cancellationToken);
                return (res, $"Đã tra cứu điểm uy tín & ý nghĩa phong thủy đầu số {prefix}", new List<ConsultationSimSuggestion>());
            }

            case "GetAuctionDetail":
            {
                var plugin = sp.GetRequiredService<AuctionPlugin>();
                string code = arguments.TryGetProperty("auctionCode", out var a) ? a.GetString() ?? "" : "";
                var res = await plugin.GetAuctionDetailAsync(code, cancellationToken);
                return (res, $"Đã tra cứu chi tiết phiên đấu giá {code}", new List<ConsultationSimSuggestion>());
            }

            default:
                return ("{\"message\":\"Tool không tồn tại\"}", $"Thực thi công cụ {toolName}", new List<ConsultationSimSuggestion>());
        }
    }

    #endregion

    #region 4. Helper Methods, Prompt Building & REST Client

    private bool IsGeminiApiKeyConfigured()
    {
        return !string.IsNullOrWhiteSpace(_settings.ApiKey)
            && (_settings.ApiKey.StartsWith("AIzaSy", StringComparison.OrdinalIgnoreCase)
                || _settings.ApiKey.StartsWith("AQ.", StringComparison.OrdinalIgnoreCase)
                || _settings.ApiKey.Length >= 20);
    }

    private string GetPreferredModel()
    {
        return !string.IsNullOrWhiteSpace(_settings.ChatModel) ? _settings.ChatModel : "gemini-3.1-flash-lite";
    }

    private List<string> GetCandidateModels()
    {
        var configuredModel = GetPreferredModel();
        var pool = new List<string>
        {
            configuredModel,
            "gemini-3.1-flash-lite",
            "gemini-3.5-flash-lite",
            "gemini-3.6-flash",
            "gemini-flash-latest",
            "gemini-3.5-flash",
            "gemini-3.7-flash",
            "gemini-2.5-pro"
        };
        return pool.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<(OpenAiMessage Message, string UsedModel)> ExecuteLlmWithFailoverAsync(
        List<OpenAiMessage> messages,
        bool includeTools,
        int maxTokens = 1500,
        float temperature = 0.2f,
        CancellationToken cancellationToken = default)
    {
        var candidateModels = GetCandidateModels();
        Exception? lastEx = null;

        foreach (var model in candidateModels)
        {
            try
            {
                var (choice, _) = await CallOpenAiEndpointAsync(
                    model,
                    messages,
                    includeTools,
                    maxTokens,
                    temperature,
                    cancellationToken);

                if ((choice.ToolCalls != null && choice.ToolCalls.Count > 0) || !string.IsNullOrWhiteSpace(choice.Content))
                {
                    return (choice, model);
                }
            }
            catch (Exception ex)
            {
                lastEx = ex;
                _logger.LogWarning("[LLM CANDIDATE FAILED] Model {Model} thất bại: {Msg}. Thử model tiếp theo trong pool...", model, ex.Message);
            }
        }

        if (lastEx != null)
        {
            throw lastEx;
        }

        throw new InvalidOperationException("Không thể nhận phản hồi từ bất kỳ mô hình AI nào trong danh sách candidates.");
    }

    private string BuildConsultationSystemPrompt(
        int? userBirthYear,
        string? userCareer,
        string? fengShuiElement,
        decimal? budgetMin,
        decimal? budgetMax)
    {
        return $@"Bạn là Chuyên gia Cao cấp Tư vấn SIM Số đẹp, Phong thủy & Thẩm định Giá tại Sàn Giao dịch TrustSim Việt Nam (TrustSim.vn).
Bạn có quyền truy cập vào 16 Native Tools để thực thi nghiệp vụ thời gian thực:
1. 'SearchAvailableSims(carrier, prefix, keyword, minPrice, maxPrice)': Quét kho SIM thực tế mở bán trên sàn.
2. 'GetSimDetails(phoneNumber)': Tra cứu chi tiết một số SIM cụ thể trên sàn.
3. 'EvaluateSimPrice(phoneNumber)': Thẩm định giá SIM 3 kênh (Showroom, P2P, Thợ thu 24h), nhịp ngắt và điểm nút.
4. 'GetMarketPriceComparables(categoryCode, prefix)': Tra cứu khung giá thị trường tham chiếu, thanh khoản theo thể loại (TU_QUY, NGU_QUY, TAM_HOA, THAN_TAI...).
5. 'GetSimCategories()': Liệt kê các thể loại SIM và hệ số nhân giá trị.
6. 'GetCarrierInfo(carrierName)': Tra cứu nhà mạng, thủ tục sang tên đổi chủ chính chủ.
7. 'GetPrefixInfo(prefixValue)': Tra cứu độ uy tín & ý nghĩa phong thủy đầu số (098, 090, 091, 086...).
8. 'GetFengShuiGuidelineByBirthYear(birthYear)': Tra cứu bản mệnh ngũ hành, quái số, số hợp mệnh theo năm sinh.
9. 'QueryFengShuiKnowledge(query)': Truy vấn tri thức RAG phong thủy chuyên sâu (49 53, đuôi số, số học).
10. 'AnalyzeSimCadence(phoneNumber)': Bóc tách thế nhịp ngắt (2.2.2.2.2, 3.3.4) và âm luật.
11. 'GetActiveAuctions(status)': Lấy danh sách tất cả các phiên đấu giá SIM VIP (ACTIVE: đang diễn ra, SCHEDULE: sắp diễn ra, hoặc null để lấy toàn bộ).
12. 'GetAuctionDetail(auctionCode)': Chi tiết bước giá, tiền cọc phiên đấu giá.
13. 'GetEscrowAndSafetyPolicy()': Chính sách ký quỹ giao dịch đảm bảo TrustSim Escrow an toàn 100%.
14. 'CalculateTradeFees(price)': Tính toán biểu phí sàn và tiền đặt cọc cho một mức giá.
15. 'GetMyWalletBalance()': Tra cứu số dư ví khả dụng của người dùng.
16. 'GetSePayDepositGuideline(amount)': Hướng dẫn nạp tiền tự động SePay VietQR.

QUY TẮC BẮT BUỘC KHI GỌI FUNCTION:
- Khi khách hỏi tìm/mua/gợi ý SIM: BẮT BUỘC gọi 'SearchAvailableSims'.
- Khi khách hỏi định giá / thẩm định giá 1 số điện thoại cụ thể: BẮT BUỘC gọi 'EvaluateSimPrice'.
- Khi khách hỏi khung giá thị trường theo dòng SIM (Tứ quý 8, Ngũ quý...): BẮT BUỘC gọi 'GetMarketPriceComparables'.
- Khi khách hỏi phân loại các thể loại SIM & hệ số nhân: BẮT BUỘC gọi 'GetSimCategories'.
- Khi khách hỏi thủ tục sang tên, địa điểm, giấy tờ nhà mạng: BẮT BUỘC gọi 'GetCarrierInfo'.
- Khi khách hỏi ý nghĩa / độ uy tín đầu số (098, 038, 090...): BẮT BUỘC gọi 'GetPrefixInfo'.
- Khi khách hỏi năm sinh / mệnh / số hợp tuổi: BẮT BUỘC gọi 'GetFengShuiGuidelineByBirthYear'.
- Khi khách hỏi kiến thức phong thủy sâu (49 53, ý nghĩa số...): BẮT BUỘC gọi 'QueryFengShuiKnowledge'.
- Khi khách hỏi thế nhịp / âm luật / ngắt số: BẮT BUỘC gọi 'AnalyzeSimCadence'.
- Khi khách hỏi về đấu giá hoặc số lượng SIM đang đấu giá: BẮT BUỘC gọi 'GetActiveAuctions'.
- BẮT BUỘC ĐỌC KỸ và LIỆT KÊ ĐẦY ĐỦ 100% tất cả các phiên đấu giá / số SIM trả về từ Tool (không được tự ý bỏ sót hoặc tóm tắt thiếu bản ghi nào), phân tách rõ ràng giữa các phiên đang diễn ra (ACTIVE) và các phiên đã lên lịch sắp diễn ra (SCHEDULE).
- Khi khách hỏi về an toàn / ký quỹ Escrow: BẮT BUỘC gọi 'GetEscrowAndSafetyPolicy'.
- Khi khách hỏi phí sàn / tiền cọc cho mức giá: BẮT BUỘC gọi 'CalculateTradeFees'.
- Khi khách hỏi số dư ví / nạp tiền: BẮT BUỘC gọi 'GetMyWalletBalance' hoặc 'GetSePayDepositGuideline'.

Thông tin khách hàng đã biết: Năm sinh: {userBirthYear?.ToString() ?? "Chưa rõ"}, Nghề nghiệp: {userCareer ?? "Chưa rõ"}, Mệnh: {fengShuiElement ?? "Chưa rõ"}, Ngân sách: {(budgetMin.HasValue ? budgetMin.Value.ToString("N0") : "0")} - {(budgetMax.HasValue ? budgetMax.Value.ToString("N0") : "Không giới hạn")} VND.
Luôn trả lời với giọng văn chuyên nghiệp, thanh lịch, giải thích rõ ràng và uy tín của Chuyên gia Viễn thông.";
    }

    private async Task<string> ExecuteChatWithCandidateModelsAsync(
        List<OpenAiMessage> messages,
        bool includeTools,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        var (choice, _) = await ExecuteLlmWithFailoverAsync(
            messages,
            includeTools,
            maxTokens,
            temperature: 0.2f,
            cancellationToken: cancellationToken);

        return CleanLlmResponse(choice.Content ?? string.Empty);
    }

    private async Task<(OpenAiMessage Message, string RawResponse)> CallOpenAiEndpointAsync(
        string model,
        List<OpenAiMessage> messages,
        bool includeTools = false,
        int maxTokens = 1500,
        float temperature = 0.25f,
        CancellationToken cancellationToken = default)
    {
        var payloadObj = new Dictionary<string, object>
        {
            { "model", model },
            { "messages", messages },
            { "temperature", temperature },
            { "max_tokens", maxTokens }
        };

        if (includeTools)
        {
            payloadObj["tools"] = AgentTools;
            payloadObj["tool_choice"] = "auto";
        }

        var json = JsonSerializer.Serialize(payloadObj);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://generativelanguage.googleapis.com/v1beta/openai/chat/completions");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        var response = await _httpClient.SendAsync(request, cts.Token);
        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[DIRECT REST ERROR] Model {Model} status {Status}: {Body}", model, response.StatusCode, responseBody);
            throw new InvalidOperationException($"Gemini API error ({response.StatusCode}): {responseBody}");
        }

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            var msgElem = firstChoice.GetProperty("message");
            var openAiMsg = JsonSerializer.Deserialize<OpenAiMessage>(msgElem.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new OpenAiMessage();
            return (openAiMsg, responseBody);
        }

        throw new InvalidOperationException("Gemini response did not contain choices");
    }

    private static string ExtractJsonString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";

        var match = Regex.Match(text, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return RepairJson(match.Groups[1].Value.Trim());
        }

        int firstBrace = text.IndexOf('{');
        int lastBrace = text.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace > firstBrace)
        {
            return RepairJson(text.Substring(firstBrace, lastBrace - firstBrace + 1).Trim());
        }

        if (firstBrace >= 0)
        {
            return RepairJson(text.Substring(firstBrace).Trim());
        }

        return text.Trim();
    }

    private static string RepairJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";
        json = json.Trim();

        int openBraces = json.Count(c => c == '{');
        int closeBraces = json.Count(c => c == '}');

        int quoteCount = 0;
        bool escaped = false;
        foreach (char c in json)
        {
            if (c == '\\') { escaped = !escaped; continue; }
            if (c == '"' && !escaped) quoteCount++;
            escaped = false;
        }

        if (quoteCount % 2 != 0)
        {
            json += "\"";
        }

        while (closeBraces < openBraces)
        {
            json += "}";
            closeBraces++;
        }

        return json;
    }

    private static string CleanLlmResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        var cleaned = Regex.Replace(raw, @"<thought>[\s\S]*?</thought>", "", RegexOptions.IgnoreCase);
        return cleaned.Trim();
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

    private async Task<AgentLoopResult> ExecuteSmartLocalAgentAsync(
        string userMessage,
        int? userBirthYear,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var result = new AgentLoopResult();
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var lower = userMessage.ToLowerInvariant();

        // 1. Phone number extraction
        var phoneMatch = Regex.Match(userMessage, @"(?:\+84|0)[3|5|7|8|9][0-9]{8}|0[0-9]{9}");
        string? extractedPhone = phoneMatch.Success ? phoneMatch.Value : null;

        // 2. Birth year extraction
        var yearMatch = Regex.Match(userMessage, @"\b(19[5-9][0-9]|20[0-2][0-9])\b");
        int? extractedYear = userBirthYear ?? (yearMatch.Success && int.TryParse(yearMatch.Value, out var y) ? y : null);

        // 3. Price extraction
        var priceMatch = Regex.Match(userMessage, @"(\d+)\s*(?:triệu|tr|trieu|tỷ|ty|vnd|đ|k)", RegexOptions.IgnoreCase);
        decimal? extractedPrice = null;
        if (priceMatch.Success && decimal.TryParse(priceMatch.Groups[1].Value, out var pVal))
        {
            if (lower.Contains("tỷ") || lower.Contains("ty")) extractedPrice = pVal * 1_000_000_000m;
            else extractedPrice = pVal * 1_000_000m;
        }

        // --- CASE 1: Định giá SIM & Phân tích thế nhịp ---
        if (!string.IsNullOrEmpty(extractedPhone) && (lower.Contains("định giá") || lower.Contains("giá") || lower.Contains("bán được") || lower.Contains("thẩm định") || lower.Contains("bao nhiêu tiền") || lower.Contains("phân khúc")))
        {
            var valPlugin = sp.GetRequiredService<SimValuationPlugin>();
            var cadencePlugin = sp.GetRequiredService<SimCadencePlugin>();

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimValuationPlugin",
                ToolSummary = $"Đang thẩm định giá trị và bóc tách đặc trưng số {extractedPhone}..."
            });
            var valJson = valPlugin.EvaluateSimPrice(extractedPhone);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimValuationPlugin",
                ToolSummary = $"Đã thẩm định thành công số {extractedPhone} theo 3 kênh thị trường"
            });

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimCadencePlugin",
                ToolSummary = $"Đang phân tích thế nhịp ngắt & âm luật số {extractedPhone}..."
            });
            var cadJson = cadencePlugin.AnalyzeSimCadence(extractedPhone);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimCadencePlugin",
                ToolSummary = $"Đã bóc tách thế nhịp và phân tích cấu trúc số {extractedPhone}"
            });

            using var vDoc = JsonDocument.Parse(valJson);
            var vRoot = vDoc.RootElement;
            string formatted = vRoot.TryGetProperty("formattedNumber", out var fn) ? fn.GetString() ?? extractedPhone : extractedPhone;
            string carrier = vRoot.TryGetProperty("carrier", out var cr) ? cr.GetString() ?? "Viễn thông" : "Viễn thông";
            string prefix = vRoot.TryGetProperty("prefix", out var pr) ? pr.GetString() ?? "" : "";
            int points = vRoot.TryGetProperty("totalPoints", out var pt) ? pt.GetInt32() : 5;
            string rhythmPattern = vRoot.TryGetProperty("rhythm", out var rh) && rh.TryGetProperty("pattern", out var rhp) ? rhp.GetString() ?? "3.3.4" : "3.3.4";
            string rhythmName = rh.TryGetProperty("name", out var rhn) ? rhn.GetString() ?? "Số đẹp" : "Số đẹp";

            decimal retailP = vRoot.TryGetProperty("priceEstimates", out var pe) && pe.TryGetProperty("retailPrice", out var retP) ? retP.GetDecimal() : 0;
            decimal p2pP = pe.TryGetProperty("marketP2PPrice", out var pP) ? pP.GetDecimal() : 0;
            decimal dealerP = pe.TryGetProperty("dealerBuybackPrice", out var dP) ? dP.GetDecimal() : 0;

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Chuyên gia Thẩm định TrustSim xin gửi tới bạn kết quả thẩm định giá trị chi tiết cho số **{formatted}**:\n");
            sb.AppendLine("### 1. Phân Tích Cấu Trúc & Thế Số");
            sb.AppendLine($"- **Nhà mạng & Đầu số:** {carrier} (Đầu số {prefix}).");
            sb.AppendLine($"- **Thế nhịp ngắt tối ưu:** **{rhythmPattern}** (`{formatted}`) - {rhythmName}.");
            sb.AppendLine($"- **Tổng nút (Điểm số phong thủy):** **{points}/10 nút**.");

            if (vRoot.TryGetProperty("categories", out var cats) && cats.GetArrayLength() > 0)
            {
                var catNames = cats.EnumerateArray().Select(c => c.GetProperty("name").GetString()).Where(s => !string.IsNullOrEmpty(s));
                sb.AppendLine($"- **Các thế số & tổ hợp tài lộc:** {string.Join(", ", catNames)}.");
            }

            sb.AppendLine("\n### 2. Bảng Định Giá 3 Kênh Thị Trường (Tham chiếu Sim Thăng Long & Chợ Sim 24h)");
            sb.AppendLine($"- **Giá Niêm Yết Showroom / Đại Lý Lớn:** ~**{retailP:N0} VNĐ** (Cam kết bảo hành, hỗ trợ hợp đồng & trả góp).");
            sb.AppendLine($"- **Giá Chợ Tự Do P2P (Sang tay trực tiếp):** ~**{p2pP:N0} VNĐ** (Mức giá chuyển nhượng hợp lý nhất).");
            sb.AppendLine($"- **Giá Thợ Thu Mua 24h Tiền Mặt:** ~**{dealerP:N0} VNĐ** (Chiết khấu an toàn thanh lý nhanh).");

            sb.AppendLine("\n### 3. Đánh Giá Thanh Khoản & Khuyến Nghị");
            if (p2pP > 500_000_000)
            {
                sb.AppendLine("Số SIM thuộc **Phân khúc Siêu VIP Cực Phẩm**, có thanh khoản rất cao trong giới doanh nhân, lãnh đạo và các nhà đầu tư số đẹp. Bạn có thể mở phiên đấu giá trên TrustSim để tối đa hóa lợi nhuận.");
            }
            else if (p2pP > 20_000_000)
            {
                sb.AppendLine("Số SIM thuộc **Phân khúc VIP Doanh nhân / Hotline**, nhu cầu thị trường ổn định, dễ dàng chuyển nhượng qua cơ chế ký quỹ TrustSim Escrow.");
            }
            else
            {
                sb.AppendLine("Số SIM thuộc **Phân khúc Phổ thông / Số đẹp Tầm trung**, dễ sử dụng làm số liên lạc cá nhân hoặc buôn bán kinh doanh hàng ngày.");
            }

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 1B: Tra cứu Khung giá Thị trường Tham chiếu theo Thể loại ---
        if (lower.Contains("khung giá") || lower.Contains("biên độ") || lower.Contains("dao động") || (lower.Contains("thị trường") && (lower.Contains("tứ quý") || lower.Contains("ngũ quý") || lower.Contains("tam hoa") || lower.Contains("thần tài") || lower.Contains("lộc phát"))))
        {
            var marketPlugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
            string catCode = lower.Contains("ngũ quý") ? "NGU_QUY" : lower.Contains("tam hoa") ? "TAM_HOA" : lower.Contains("thần tài") ? "THAN_TAI" : lower.Contains("lộc phát") ? "LOC_PHAT" : "TU_QUY";
            string pref = lower.Contains("090") ? "090" : lower.Contains("091") ? "091" : "098";

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimMarketHistoryPlugin",
                ToolSummary = $"Đang tra cứu khung giá thị trường tham chiếu cho dòng {catCode} đầu {pref}..."
            });
            var compJson = await marketPlugin.GetMarketPriceComparablesAsync(catCode, pref, cancellationToken);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimMarketHistoryPlugin",
                ToolSummary = $"Đã tra cứu biểu giá thị trường dòng {catCode}"
            });

            using var cDoc = JsonDocument.Parse(compJson);
            var cRoot = cDoc.RootElement;
            string catName = cRoot.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "SIM Số Đẹp" : "SIM Số Đẹp";
            string p2pRange = cRoot.TryGetProperty("marketP2PRange", out var p2r) ? p2r.GetString() ?? "120tr - 380tr VNĐ" : "120tr - 380tr VNĐ";
            string retailRange = cRoot.TryGetProperty("showroomRetailRange", out var srr) ? srr.GetString() ?? "150tr - 500tr VNĐ" : "150tr - 500tr VNĐ";
            string dealerRange = cRoot.TryGetProperty("dealerBuybackRange", out var dbr) ? dbr.GetString() ?? "80tr - 250tr VNĐ" : "80tr - 250tr VNĐ";
            string liquidity = cRoot.TryGetProperty("liquidityComment", out var lc) ? lc.GetString() ?? "Thanh khoản rất cao trong 24h - 48h" : "Thanh khoản rất cao trong 24h - 48h";

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Dưới đây là khung giá thị trường tham chiếu cho dòng **{catName} (Đầu số {pref})**:\n");
            sb.AppendLine("### 1. Khung Giá Giao Dịch Thực Tế 3 Kênh:");
            sb.AppendLine($"- **Giá Niêm Yết Showroom / Đại Lý Lớn:** **{retailRange}** (Kèm bảo hành trọn đời, hợp đồng pháp lý & trả góp).");
            sb.AppendLine($"- **Giá Chợ Tự Do P2P (Sang tay Doanh nhân):** **{p2pRange}** (Mức giá giao dịch phổ biến nhất).");
            sb.AppendLine($"- **Giá Thợ Thu Mua 24h Tiền Mặt:** **{dealerRange}** (Chiết khấu an toàn thanh khoản nhanh).\n");
            sb.AppendLine("### 2. Đánh Giá Thanh Khoản & Xu Hướng Thị Trường:");
            sb.AppendLine($"- {liquidity}");
            sb.AppendLine("- Dòng SIM đầu số cổ luôn duy trì tốc độ tăng trưởng giá trị trung bình 10% - 20%/năm, là tài sản tích lũy an toàn và khẳng định vị thế đỉnh cao.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 1C: Liệt kê Thể loại SIM & Hệ số nhân ---
        if (lower.Contains("thể loại") || lower.Contains("phân loại") || lower.Contains("hệ số nhân") || lower.Contains("các dòng sim"))
        {
            var marketPlugin = sp.GetRequiredService<SimMarketHistoryPlugin>();
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimMarketHistoryPlugin",
                ToolSummary = "Đang tra cứu danh sách thể loại SIM số đẹp & hệ số định giá từ database sàn..."
            });
            var catJson = await marketPlugin.GetSimCategoriesAsync(cancellationToken);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimMarketHistoryPlugin",
                ToolSummary = "Đã lấy danh sách thể loại SIM thành công"
            });

            using var catsDoc = JsonDocument.Parse(catJson);
            var catsRoot = catsDoc.RootElement;

            var sb = new StringBuilder();
            sb.AppendLine("Chào bạn! Sàn Giao dịch TrustSim hiện đang phân loại các **Thể loại SIM Số đẹp** theo ma trận giá trị chuẩn sau:\n");
            sb.AppendLine("### Bảng Phân Loại & Hệ Số Nhân Giá Trị Thị Trường:");
            if (catsRoot.TryGetProperty("categories", out var catList) && catList.GetArrayLength() > 0)
            {
                foreach (var item in catList.EnumerateArray())
                {
                    string name = item.GetProperty("name").GetString() ?? "";
                    float multi = item.GetProperty("multiplier").GetSingle();
                    string desc = item.GetProperty("description").GetString() ?? "";
                    sb.AppendLine($"• **{name}** (Hệ số: **{multi:F1}x**) : {desc}");
                }
            }
            else
            {
                sb.AppendLine("• **Bát quý / Cửu quý** (Hệ số: 50.0x) : Độc nhất vô nhị, tài sản di sản hàng chục tỷ đồng.");
                sb.AppendLine("• **Lục quý** (Hệ số: 25.0x) : 6 số cuối giống nhau, Siêu VIP tiền tỷ.");
                sb.AppendLine("• **Ngũ quý** (Hệ số: 10.0x - 18.0x) : 5 số cuối giống nhau liên tiếp.");
                sb.AppendLine("• **Tứ quý** (Hệ số: 5.0x - 8.0x) : 4 số cuối trùng nhau.");
                sb.AppendLine("• **Sảnh rồng & Sảnh tiến** (Hệ số: 4.0x - 7.0x) : Dãy số tiến liên hoàn.");
                sb.AppendLine("• **Thần tài & Lộc phát VIP** (Hệ số: 2.5x - 4.0x) : Đuôi 7979, 3979, 6868, 8686.");
                sb.AppendLine("• **Taxi & Tam hoa** (Hệ số: 2.0x - 3.5x) : Dễ nhớ, thích hợp hotline công ty.");
            }
            sb.AppendLine("\nCác dòng có hệ số nhân cao nhất là **Bát quý, Lục quý, Ngũ quý đầu cổ và Đại Taxi 4 cặp**, sở hữu thanh khoản tức thì và giá trị tăng theo thời gian.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 1D: Phân tích Thế nhịp & Âm luật đọc ---
        if (!string.IsNullOrEmpty(extractedPhone) && (lower.Contains("thế nhịp") || lower.Contains("âm luật") || lower.Contains("ngắt") || lower.Contains("đọc") || lower.Contains("bóc tách")))
        {
            var cadencePlugin = sp.GetRequiredService<SimCadencePlugin>();
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimCadencePlugin",
                ToolSummary = $"Đang phân tích cấu trúc nhịp ngắt và âm luật cho số {extractedPhone}..."
            });
            var cadJson = cadencePlugin.AnalyzeSimCadence(extractedPhone);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimCadencePlugin",
                ToolSummary = "Đã phân tích âm luật & nhịp ngắt tối ưu"
            });

            using var cDoc = JsonDocument.Parse(cadJson);
            var cRoot = cDoc.RootElement;
            string bestFmt = cRoot.TryGetProperty("bestFormattedNumber", out var bf) ? bf.GetString() ?? extractedPhone : extractedPhone;
            string bestRhythm = cRoot.TryGetProperty("bestRhythmName", out var brn) ? brn.GetString() ?? "Số đẹp" : "Số đẹp";
            string bestPattern = cRoot.TryGetProperty("bestRhythmValue", out var brv) ? brv.GetString() ?? "3.3.4" : "3.3.4";
            string comp = cRoot.TryGetProperty("cadenceComparisonAnalysis", out var cca) ? cca.GetString() ?? "" : "";

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Dưới đây là phân tích chi tiết về **Thế nhịp ngắt & Âm luật phát âm** cho số **{extractedPhone}**:\n");
            sb.AppendLine($"### 1. Nhịp Ngắt Tối Ưu Nhất: **{bestPattern}** (`{bestFmt}`)");
            sb.AppendLine($"- **Tên cấu trúc thế số:** **{bestRhythm}**.");
            sb.AppendLine($"- **Phân tích so sánh âm luật:** {comp}\n");
            sb.AppendLine("### 2. Giá Trị Thương Hiệu & Độ Ghi Nhớ:");
            sb.AppendLine("- Nhịp phát âm điệp vần, cân đối giữa các thanh bằng trắc giúp khách hàng hoặc đối tác ghi nhớ chỉ sau 1 lần nghe hoặc nhìn lướt qua biển quảng cáo / danh thiếp.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 2: Tra cứu Phong thủy theo Năm sinh ---
        if (extractedYear.HasValue && (lower.Contains("mệnh") || lower.Contains("hợp số") || lower.Contains("năm sinh") || lower.Contains("sinh năm") || lower.Contains("phong thủy") || lower.Contains("tuổi")))
        {
            var fengShuiPlugin = sp.GetRequiredService<FengShuiKnowledgePlugin>();
            var simRepo = sp.GetRequiredService<ISimNumberRepository>();

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "FengShuiKnowledgePlugin",
                ToolSummary = $"Đang tính toán bản mệnh ngũ hành & quái số năm sinh {extractedYear.Value}..."
            });
            var fsJson = fengShuiPlugin.GetFengShuiGuidelineByBirthYear(extractedYear.Value);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "FengShuiKnowledgePlugin",
                ToolSummary = $"Đã xác định bản mệnh ngũ hành năm {extractedYear.Value}"
            });

            using var fDoc = JsonDocument.Parse(fsJson);
            var fRoot = fDoc.RootElement;
            string canChi = fRoot.TryGetProperty("canChi", out var cc) ? cc.GetString() ?? "" : "";
            string element = fRoot.TryGetProperty("element", out var el) ? el.GetString() ?? "Kim" : "Kim";
            string advice = fRoot.TryGetProperty("advice", out var ad) ? ad.GetString() ?? "" : "";

            var favNums = fRoot.TryGetProperty("favorableNumbers", out var fn)
                ? string.Join(", ", fn.EnumerateArray().Select(n => n.GetInt32().ToString()))
                : "6, 8, 9";

            var unfavNums = fRoot.TryGetProperty("unfavorableNumbers", out var un)
                ? string.Join(", ", un.EnumerateArray().Select(n => n.GetInt32().ToString()))
                : "4, 7";

            // Quét kho SIM thực tế
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "SimInventoryPlugin",
                ToolSummary = $"Đang quét kho SIM số đẹp thực tế hợp mệnh {element}..."
            });
            var sims = await simRepo.GetAvailableSimsForConsultationAsync(budgetMin, budgetMax, null, null, null, limit: 4, cancellationToken);
            var suggestions = sims.Select(s => new ConsultationSimSuggestion
            {
                SimNumberId = s.Id,
                PhoneNumber = s.PhoneNumber,
                Price = s.Price,
                CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Hợp mệnh {element}"
            }).ToList();
            result.CollectedSims.AddRange(suggestions);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "SimInventoryPlugin",
                ToolSummary = $"Tìm thấy {suggestions.Count} số SIM thực tế mở bán trên sàn"
            });

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Dưới đây là phân tích phong thủy số học chi tiết cho gia chủ sinh năm **{extractedYear.Value}**:\n");
            sb.AppendLine($"### 1. Bản Mệnh & Ngũ Hành");
            sb.AppendLine($"- **Năm sinh âm lịch:** **{canChi}**.");
            sb.AppendLine($"- **Bản mệnh ngũ hành:** **Mệnh {element}**.");
            sb.AppendLine($"- **Các con số Cát Tường (Tương sinh & Bản mệnh):** **{favNums}**.");
            sb.AppendLine($"- **Các con số Kỵ (Tương khắc cần tránh):** **{unfavNums}**.");
            sb.AppendLine($"- **Lời khuyên chọn số:** {advice}\n");

            if (suggestions.Count > 0)
            {
                sb.AppendLine("### 2. Gợi Ý Số SIM Thực Tế Hợp Mệnh Đang Mở Bán");
                foreach (var s in suggestions)
                {
                    sb.AppendLine($"• **{s.PhoneNumber}** ({s.CarrierName}) — Giá: **{s.Price:N0} đ** : {s.HighlightReason}");
                }
            }

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 3: Thủ tục sang tên & Nhà mạng ---
        if (lower.Contains("thủ tục") || lower.Contains("sang tên") || lower.Contains("đổi chủ") || lower.Contains("điểm giao dịch") || lower.Contains("giấy tờ"))
        {
            var carrierPlugin = sp.GetRequiredService<CarrierPolicyPlugin>();
            var escrowPlugin = sp.GetRequiredService<TradeAndEscrowPlugin>();

            string carrierName = lower.Contains("vina") ? "VinaPhone" : lower.Contains("mobi") ? "MobiFone" : "Viettel";

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "CarrierPolicyPlugin",
                ToolSummary = $"Đang tra cứu quy trình thủ tục sang tên mạng {carrierName}..."
            });
            var carJson = await carrierPlugin.GetCarrierInfoAsync(carrierName, cancellationToken);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "CarrierPolicyPlugin",
                ToolSummary = $"Đã lấy quy trình thủ tục sang tên nhà mạng {carrierName}"
            });

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "TradeAndEscrowPlugin",
                ToolSummary = "Đang tra cứu chính sách bảo vệ người mua TrustSim Escrow..."
            });
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "TradeAndEscrowPlugin",
                ToolSummary = "Quy trình ký quỹ an toàn 100%"
            });

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Dưới đây là hướng dẫn chi tiết về thủ tục sang tên đổi chủ chính chủ mạng **{carrierName}** trên sàn TrustSim:\n");
            sb.AppendLine("### 1. Địa Điểm & Thời Gian Thực Hiện");
            sb.AppendLine($"- **Địa điểm:** Bất kỳ cửa hàng / trung tâm giao dịch chính thức của **{carrierName}** trên toàn quốc.");
            sb.AppendLine("- **Thời gian xử lý:** Chỉ mất từ **15 – 30 phút** tại quầy giao dịch.\n");
            sb.AppendLine("### 2. Giấy Tờ Chuẩn Bị Bắt Buộc");
            sb.AppendLine("- **Bên bán (Chủ sở hữu hiện tại):** Mang theo **CCCD/CMND gốc** + **SIM vật lý / eSIM** đang hoạt động (có thể cần cung cấp 3-5 số điện thoại liên lạc gần nhất nếu nhà mạng yêu cầu xác minh).");
            sb.AppendLine("- **Bên mua (Người nhận chuyển nhượng):** Mang theo **CCCD gắn chip gốc** để đăng ký thông tin thuê bao chính chủ theo Nghị định 49/CP.\n");
            sb.AppendLine("### 3. Cơ Chế Giao Dịch Đảm Bảo TrustSim Escrow");
            sb.AppendLine("1. Người mua nạp tiền giữ trong ví ký quỹ TrustSim.");
            sb.AppendLine("2. Hai bên gặp nhau tại cửa hàng nhà mạng để làm thủ tục sang tên.");
            sb.AppendLine("3. Sau khi nhân viên nhà mạng in phiếu xác nhận đổi chủ thành công, hệ thống TrustSim tự động mở khóa giải ngân tiền cho người bán.");
            sb.AppendLine("4. An toàn tuyệt đối 100% — Cam kết hoàn tiền nếu phát sinh tranh chấp.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 4: Tra cứu Đầu số cổ / Ý nghĩa đầu số ---
        if (lower.Contains("đầu số") || lower.Contains("đầu 098") || lower.Contains("đầu 090") || lower.Contains("đầu 091") || lower.Contains("đầu 088") || lower.Contains("đầu 086"))
        {
            var carrierPlugin = sp.GetRequiredService<CarrierPolicyPlugin>();
            string prefix = lower.Contains("090") ? "090" : lower.Contains("091") ? "091" : lower.Contains("088") ? "088" : lower.Contains("086") ? "086" : "098";

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "CarrierPolicyPlugin",
                ToolSummary = $"Đang tra cứu điểm uy tín & ý nghĩa phong thủy đầu số {prefix}..."
            });
            var prefJson = await carrierPlugin.GetPrefixInfoAsync(prefix, cancellationToken);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "CarrierPolicyPlugin",
                ToolSummary = $"Đã tra cứu thông tin đầu số {prefix}"
            });

            using var pDoc = JsonDocument.Parse(prefJson);
            var pRoot = pDoc.RootElement;
            string carName = pRoot.TryGetProperty("carrierName", out var cn) ? cn.GetString() ?? "Viễn thông" : "Viễn thông";
            float repScore = pRoot.TryGetProperty("reputationScore", out var rs) ? rs.GetSingle() : 1.5f;
            string category = pRoot.TryGetProperty("category", out var cg) ? cg.GetString() ?? "Đầu số cổ" : "Đầu số cổ";
            string fengShui = pRoot.TryGetProperty("fengShuiMeaning", out var fs) ? fs.GetString() ?? "" : "";

            var sb = new StringBuilder();
            sb.AppendLine($"Chào bạn! Dưới đây là thông tin chi tiết về **Đầu số {prefix}**:\n");
            sb.AppendLine($"- **Nhà mạng sở hữu:** **{carName}**.");
            sb.AppendLine($"- **Phân loại đầu số:** **{category}**.");
            sb.AppendLine($"- **Điểm uy tín thị trường (Reputation Score):** **{repScore:F2}** (Hệ số nhân định giá cao nhất).");
            sb.AppendLine($"- **Ý nghĩa phong thủy số học:** {fengShui}");
            sb.AppendLine("\nĐầu số cổ luôn giữ giá trị ổn định nhất trên thị trường viễn thông Việt Nam, được các chính khách, doanh nhân và lãnh đạo doanh nghiệp ưu tiên lựa chọn hàng đầu.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 5: Đấu giá SIM VIP (Chi tiết hoặc Danh sách) ---
        if (lower.Contains("đấu giá") || lower.Contains("phiên") || lower.Contains("auction") || lower.Contains("auc"))
        {
            var auctionPlugin = sp.GetRequiredService<AuctionPlugin>();
            var aucMatch = Regex.Match(userMessage, @"(?i)\b(AUC[0-9A-Z]+)\b");

            if (aucMatch.Success)
            {
                string aucCode = aucMatch.Value.ToUpperInvariant();
                result.ToolEvents.Add(new ChatConsultationStreamEvent
                {
                    EventType = "tool_start",
                    ToolName = "AuctionPlugin",
                    ToolSummary = $"Đang tra cứu chi tiết quy tắc & tiền cọc phiên đấu giá {aucCode}..."
                });
                var detailJson = await auctionPlugin.GetAuctionDetailAsync(aucCode, cancellationToken);
                result.ToolEvents.Add(new ChatConsultationStreamEvent
                {
                    EventType = "tool_end",
                    ToolName = "AuctionPlugin",
                    ToolSummary = $"Đã lấy thông tin chi tiết phiên {aucCode}"
                });

                using var dDoc = JsonDocument.Parse(detailJson);
                var dRoot = dDoc.RootElement;
                if (!dRoot.TryGetProperty("error", out _))
                {
                    string phone = dRoot.TryGetProperty("phoneNumber", out var p) ? p.GetString() ?? "" : "";
                    decimal curP = dRoot.TryGetProperty("currentPrice", out var cp) ? cp.GetDecimal() : 0;
                    decimal stepP = dRoot.TryGetProperty("stepPrice", out var st) ? st.GetDecimal() : 0;
                    decimal depP = dRoot.TryGetProperty("depositAmount", out var dp) ? dp.GetDecimal() : 0;
                    int bids = dRoot.TryGetProperty("totalBids", out var tb) ? tb.GetInt32() : 0;
                    string endTime = dRoot.TryGetProperty("endTime", out var et) ? et.GetString() ?? "" : "";

                    var dSb = new StringBuilder();
                    dSb.AppendLine($"Chào bạn! Dưới đây là thông tin chi tiết của **Phiên Đấu Giá {aucCode}** trên sàn TrustSim:\n");
                    dSb.AppendLine("### 1. Thông Tin Phiên & Quy Tắc Trả Giá:");
                    dSb.AppendLine($"- **Số SIM đấu giá:** **{phone}**.");
                    dSb.AppendLine($"- **Giá trả hiện tại:** **{curP:N0} VNĐ** (Tổng số lượt trả: {bids} lượt).");
                    dSb.AppendLine($"- **Bước giá tối thiểu (`stepPrice`):** **{stepP:N0} VNĐ**.");
                    dSb.AppendLine($"- **Tiền đặt cọc tham gia (`depositAmount`):** **{depP:N0} VNĐ**.");
                    dSb.AppendLine($"- **Thời hạn kết thúc phiên:** **{endTime}**.\n");
                    dSb.AppendLine("### 2. Hướng Dẫn Tham Gia:");
                    dSb.AppendLine("Bạn vui lòng nạp đủ tiền đặt cọc vào ví trước khi phiên kết thúc. Tiền cọc sẽ được hoàn lại 100% ngay sau khi phiên đấu giá khép lại nếu bạn không phải người trả giá cao nhất.");

                    result.ReplyContent = dSb.ToString();
                    return result;
                }
            }

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "AuctionPlugin",
                ToolSummary = "Đang quét danh sách phiên đấu giá SIM VIP trực tiếp trên sàn..."
            });
            var aucJson = await auctionPlugin.GetActiveAuctionsAsync(null, cancellationToken);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "AuctionPlugin",
                ToolSummary = "Đã tra cứu dữ liệu phiên đấu giá TrustSim"
            });

            using var aDoc = JsonDocument.Parse(aucJson);
            var aRoot = aDoc.RootElement;
            int total = aRoot.TryGetProperty("total", out var t) ? t.GetInt32() : 0;

            var sb = new StringBuilder();
            sb.AppendLine("Chào bạn! Dưới đây là thông tin các phiên đấu giá SIM VIP trực tiếp trên sàn TrustSim:\n");

            if (total > 0 && aRoot.TryGetProperty("items", out var items) && items.GetArrayLength() > 0)
            {
                sb.AppendLine("### Danh Sách Phiên Đấu Giá Đang Mở:");
                foreach (var item in items.EnumerateArray())
                {
                    string code = item.GetProperty("auctionCode").GetString() ?? "";
                    string phone = item.GetProperty("phoneNumber").GetString() ?? "";
                    decimal curPrice = item.GetProperty("currentPrice").GetDecimal();
                    decimal step = item.GetProperty("stepPrice").GetDecimal();
                    string end = item.GetProperty("endTime").GetString() ?? "";
                    sb.AppendLine($"• **Mã: {code}** — Số SIM: **{phone}** — Giá hiện tại: **{curPrice:N0} đ** (Bước giá: {step:N0} đ) — Kết thúc: {end}");
                }
                sb.AppendLine("\nBạn có thể nộp tiền đặt cọc vào ví để tham gia trả giá trực tiếp.");
            }
            else
            {
                sb.AppendLine("Hiện tại các phiên đấu giá tuần này đang trong giai đoạn chuẩn bị mở đăng ký. Bạn có thể theo dõi sảnh Đấu Giá trên thanh điều hướng để cập nhật các siêu phẩm SIM mới nhất!");
            }

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 6: Ký quỹ Escrow & Tính phí sàn ---
        if (lower.Contains("ký quỹ") || lower.Contains("escrow") || lower.Contains("an toàn") || lower.Contains("lừa đảo") || lower.Contains("phí sàn") || lower.Contains("đặt cọc") || lower.Contains("bảo đảm"))
        {
            var escrowPlugin = sp.GetRequiredService<TradeAndEscrowPlugin>();
            decimal priceToCalc = extractedPrice ?? 200_000_000m;

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "TradeAndEscrowPlugin",
                ToolSummary = $"Đang tính toán biểu phí sàn và chính sách ký quỹ cho mức giá {priceToCalc:N0} đ..."
            });
            var feeJson = escrowPlugin.CalculateTradeFees(priceToCalc);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "TradeAndEscrowPlugin",
                ToolSummary = "Đã tính toán biểu phí sàn & tiền đặt cọc thành công"
            });

            using var feeDoc = JsonDocument.Parse(feeJson);
            var feeRoot = feeDoc.RootElement;
            string feeRate = feeRoot.TryGetProperty("feeRatePercentage", out var fr) ? fr.GetString() ?? "3%" : "3%";
            decimal feeAmount = feeRoot.TryGetProperty("feeAmount", out var fa) ? fa.GetDecimal() : (priceToCalc * 0.03m);
            decimal depRequired = feeRoot.TryGetProperty("depositRequired", out var dr) ? dr.GetDecimal() : (priceToCalc * 0.10m);
            decimal netSeller = feeRoot.TryGetProperty("netSellerReceives", out var ns) ? ns.GetDecimal() : (priceToCalc - feeAmount);

            var sb = new StringBuilder();
            sb.AppendLine("Chào bạn! Dưới đây là thông tin chi tiết về **Chính sách Ký quỹ TrustSim Escrow & Biểu phí sàn**:\n");
            sb.AppendLine($"### 1. Bảng Tính Phí Giao Dịch & Đặt Cọc (Ví dụ mức giá: {priceToCalc:N0} VNĐ)");
            sb.AppendLine($"- **Tỷ lệ phí sàn áp dụng:** **{feeRate}** ({feeAmount:N0} VNĐ).");
            sb.AppendLine($"- **Tiền đặt cọc Người mua nộp giữ chân (10%):** **{depRequired:N0} VNĐ**.");
            sb.AppendLine($"- **Số tiền Người bán thực nhận sau khi hoàn tất:** **{netSeller:N0} VNĐ**.\n");
            sb.AppendLine("### 2. Cơ Chế Bảo Vệ An Toàn 100%");
            sb.AppendLine("- Toàn bộ tiền cọc và tiền mua SIM được lưu trữ độc lập tại tài khoản ngân hàng ký quỹ.");
            sb.AppendLine("- Người bán chỉ nhận được tiền khi hai bên hoàn tất thủ tục sang tên chính chủ tại điểm giao dịch nhà mạng và hệ thống xác thực thành công.");
            sb.AppendLine("- Nếu bên bán đơn phương hủy giao dịch hoặc sim có tranh chấp, tiền cọc sẽ được hoàn trả 100% ngay lập tức cho người mua.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- CASE 7: Số dư ví & Nạp tiền SePay ---
        if (lower.Contains("số dư") || lower.Contains("nạp tiền") || lower.Contains("ví") || lower.Contains("sepay") || lower.Contains("vietqr") || lower.Contains("chuyển khoản"))
        {
            var walletPlugin = sp.GetRequiredService<UserWalletPlugin>();
            decimal depositAmount = extractedPrice ?? 50_000_000m;

            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_start",
                ToolName = "UserWalletPlugin",
                ToolSummary = "Đang kiểm tra số dư ví & thông tin cổng nạp tự động SePay VietQR..."
            });
            var balJson = await walletPlugin.GetMyWalletBalanceAsync(cancellationToken);
            var sepJson = walletPlugin.GetSePayDepositGuideline(depositAmount);
            result.ToolEvents.Add(new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "UserWalletPlugin",
                ToolSummary = "Đã lấy thông tin nạp tiền tự động SePay VietQR"
            });

            using var sDoc = JsonDocument.Parse(sepJson);
            var sRoot = sDoc.RootElement;
            string bank = sRoot.TryGetProperty("bankName", out var b) ? b.GetString() ?? "MBBank" : "MBBank";
            string accNum = sRoot.TryGetProperty("accountNumber", out var an) ? an.GetString() ?? "" : "";
            string accHolder = sRoot.TryGetProperty("accountHolder", out var ah) ? ah.GetString() ?? "CONG TY TNHH TRUSTSIM" : "CONG TY TNHH TRUSTSIM";
            string syntax = sRoot.TryGetProperty("transferSyntax", out var sx) ? sx.GetString() ?? "TS NAPTIEN" : "TS NAPTIEN";

            var sb = new StringBuilder();
            sb.AppendLine("Chào bạn! Dưới đây là hướng dẫn nạp tiền tự động vào ví sàn TrustSim qua **SePay VietQR 24/7**:\n");
            sb.AppendLine("### Thông Tin Chuyển Khoản Nạp Tiền:");
            sb.AppendLine($"- **Ngân hàng thụ hưởng:** **{bank}**");
            sb.AppendLine($"- **Số tài khoản:** **{accNum}**");
            sb.AppendLine($"- **Chủ tài khoản:** **{accHolder}**");
            sb.AppendLine($"- **Số tiền nạp đề xuất:** **{depositAmount:N0} VNĐ**");
            sb.AppendLine($"- **Nội dung chuyển khoản (Bắt buộc chính xác):** `{syntax}`\n");
            sb.AppendLine("⚡ **Thời gian xử lý:** Hệ thống Webhook SePay sẽ tự động cộng số dư vào ví của bạn trong vòng **5 – 15 giây** ngay sau khi chuyển khoản thành công.");

            result.ReplyContent = sb.ToString();
            return result;
        }

        // --- DEFAULT: Quét kho SIM theo tiêu chí chung ---
        var invPlugin = sp.GetRequiredService<SimInventoryPlugin>();
        result.ToolEvents.Add(new ChatConsultationStreamEvent
        {
            EventType = "tool_start",
            ToolName = "SimInventoryPlugin",
            ToolSummary = "Đang quét kho SIM trên sàn TrustSim..."
        });

        var defaultSims = await sp.GetRequiredService<ISimNumberRepository>()
            .GetAvailableSimsForConsultationAsync(budgetMin, budgetMax, null, null, null, limit: 5, cancellationToken);

        var defaultSuggs = defaultSims.Select(s => new ConsultationSimSuggestion
        {
            SimNumberId = s.Id,
            PhoneNumber = s.PhoneNumber,
            Price = s.Price,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
            HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
        }).ToList();
        result.CollectedSims.AddRange(defaultSuggs);

        result.ToolEvents.Add(new ChatConsultationStreamEvent
        {
            EventType = "tool_end",
            ToolName = "SimInventoryPlugin",
            ToolSummary = $"Tìm thấy {defaultSuggs.Count} số SIM thực tế trong kho"
        });

        var defSb = new StringBuilder();
        defSb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        defSb.AppendLine("Dựa trên thông tin và tiêu chí tìm kiếm của bạn, tôi xin gợi ý các định hướng chọn số mang lại vượng khí tốt nhất:");
        defSb.AppendLine("- Nên ưu tiên các đầu số cổ uy tín của các nhà mạng lớn (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) để khẳng định vị thế.");
        defSb.AppendLine("- Kết hợp các đuôi số phong thủy kích hoạt tài lộc như Thần tài (39, 79), Lộc phát (68, 86) hoặc số tiến (6789, 1368).\n");

        if (defaultSuggs.Count > 0)
        {
            defSb.AppendLine("Tôi đã rà soát kho SIM thực tế trên sàn TrustSim và tuyển chọn các số đẹp phù hợp nhất dưới đây:");
            foreach (var sim in defaultSuggs)
            {
                defSb.AppendLine($"• **{sim.PhoneNumber}** ({sim.CarrierName}) — Giá: **{sim.Price:N0} đ** : {sim.HighlightReason}");
            }
        }

        result.ReplyContent = defSb.ToString();
        return result;
    }

    private async Task<AiConsultationLlmResponse> ExecuteFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var smartRes = await ExecuteSmartLocalAgentAsync(userMessage, null, budgetMin, budgetMax, cancellationToken);
        return new AiConsultationLlmResponse
        {
            ReplyContent = smartRes.ReplyContent,
            SuggestedSims = smartRes.CollectedSims,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = smartRes.ReferencedDocs.Distinct().ToList(),
            TokensUsed = (int)(smartRes.ReplyContent.Length / 3.5),
            EngineSource = "LOCAL_FALLBACK_ENGINE"
        };
    }

    private async IAsyncEnumerable<ChatConsultationStreamEvent> StreamFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var smartRes = await ExecuteSmartLocalAgentAsync(userMessage, null, budgetMin, budgetMax, cancellationToken);

        foreach (var evt in smartRes.ToolEvents)
        {
            yield return evt;
            await Task.Delay(25, cancellationToken);
        }

        var words = smartRes.ReplyContent.Split(new[] { ' ' }, StringSplitOptions.None);
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

        yield return new ChatConsultationStreamEvent
        {
            EventType = "done",
            SuggestedSims = smartRes.CollectedSims,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = smartRes.ReferencedDocs.Distinct().ToList(),
            EngineSource = "LOCAL_FALLBACK_ENGINE"
        };
    }


    #endregion

    #region 5. Data Models & Tool Definitions

    private class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<OpenAiToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ToolCallId { get; set; }

        [JsonPropertyName("extra_content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ExtraContent { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class OpenAiToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAiFunctionCall Function { get; set; } = new();

        [JsonPropertyName("extra_content")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public object? ExtraContent { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private class OpenAiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "{}";

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }

    private static readonly object[] AgentTools = new object[]
    {
        new
        {
            type = "function",
            function = new
            {
                name = "SearchAvailableSims",
                description = "Tìm kiếm danh sách số SIM thực tế đang mở bán trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số. CHỈ gọi khi người dùng muốn tìm SIM mua hoặc cần gợi ý danh sách số có thể mua.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        carrier = new { type = "string", description = "Tên nhà mạng (Viettel, VinaPhone, MobiFone, Vietnamobile, Wintel, ITelecom) nếu người dùng có yêu cầu." },
                        prefix = new { type = "string", description = "Đầu số cụ thể (ví dụ: 098, 090, 091, 088, 086...) nếu người dùng có yêu cầu." },
                        keyword = new { type = "string", description = "Thế số hoặc đuôi số cần tìm (ví dụ: 6868, 7979, 9999, 1368, 888...) nếu người dùng có yêu cầu." },
                        minPrice = new { type = "number", description = "Giá tối thiểu bằng VNĐ nếu có." },
                        maxPrice = new { type = "number", description = "Giá tối đa bằng VNĐ nếu có." }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetSimDetails",
                description = "Lấy thông tin chi tiết một số SIM cụ thể trên sàn TrustSim.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần xem chi tiết (ví dụ: 0986868686)" }
                    },
                    required = new[] { "phoneNumber" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "QueryFengShuiKnowledge",
                description = "Truy vấn kho tri thức RAG phong thủy ngũ hành, can chi và cẩm nang số học từ TrustSim. CHỈ gọi khi người dùng hỏi về kiến thức phong thủy, hợp mệnh, ý nghĩa đuôi số.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Câu hỏi hoặc từ khóa tìm kiếm phong thủy" }
                    },
                    required = new[] { "query" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetFengShuiGuidelineByBirthYear",
                description = "Tra cứu bản mệnh ngũ hành, quái số và các cặp số cát tường tương sinh theo năm sinh dương lịch.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        birthYear = new { type = "integer", description = "Năm sinh dương lịch của khách hàng (ví dụ: 2004, 1990)" }
                    },
                    required = new[] { "birthYear" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetActiveAuctions",
                description = "Lấy danh sách tất cả các phiên đấu giá SIM số đẹp VIP (ACTIVE: đang diễn ra, SCHEDULE: sắp diễn ra) trên sàn TrustSim. Gọi khi khách hỏi về đấu giá hoặc số lượng sim đang đấu giá.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        status = new { type = "string", description = "Trạng thái: 'ACTIVE', 'SCHEDULE', hoặc để trống để lấy tất cả" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetEscrowAndSafetyPolicy",
                description = "Giải thích quy trình giao dịch đảm bảo (Escrow), giữ tiền an toàn 100% và bảo vệ người mua trên TrustSim.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "CalculateTradeFees",
                description = "Tính toán chính xác phí sàn và tiền đặt cọc cần thiết khi mua hoặc đấu giá một số SIM.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        price = new { type = "number", description = "Mức giá giao dịch của số SIM (VND)" }
                    },
                    required = new[] { "price" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetMyWalletBalance",
                description = "Tra cứu số dư ví khả dụng hiện tại của người dùng đã đăng nhập.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetSePayDepositGuideline",
                description = "Cung cấp thông tin và hướng dẫn nạp tiền vào ví sàn TrustSim qua SePay VietQR.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        amount = new { type = "number", description = "Số tiền muốn nạp vào ví (VND)" }
                    },
                    required = new[] { "amount" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "AnalyzeSimCadence",
                description = "Bóc tách thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần bóc tách phân tích" }
                    },
                    required = new[] { "phoneNumber" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "EvaluateSimPrice",
                description = "Định giá nhanh một số điện thoại dựa trên thế nhịp ngắt, uy tín đầu số và bóc tách đặc trưng số học. Gọi khi người dùng hỏi số này giá bao nhiêu hoặc yêu cầu thẩm định giá.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        phoneNumber = new { type = "string", description = "Số điện thoại cần định giá (ví dụ: 0988888888, 0903123456)" }
                    },
                    required = new[] { "phoneNumber" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetMarketPriceComparables",
                description = "Tra cứu khung giá thị trường tham chiếu, khoảng giá giao dịch thực tế và biên độ thanh khoản theo thể loại số SIM (Tứ quý, Tam hoa, Thần tài, Lộc phát...) và đầu số.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        categoryCode = new { type = "string", description = "Mã thể loại SIM (ví dụ: TU_QUY, TAM_HOA, THAN_TAI, LOC_PHAT, SANH_TIEN, TAXI, NGU_QUY)" },
                        prefix = new { type = "string", description = "Đầu số nhà mạng (ví dụ: 098, 090, 091, 088...)" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetSimCategories",
                description = "Lấy danh sách các thể loại SIM số đẹp phổ biến trên sàn TrustSim kèm hệ số nhân giá trị.",
                parameters = new
                {
                    type = "object",
                    properties = new { }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetCarrierInfo",
                description = "Tra cứu thông tin chi tiết về nhà mạng viễn thông (Viettel, VinaPhone, MobiFone...), danh sách đầu số và quy trình thủ tục sang tên chính chủ.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        carrierName = new { type = "string", description = "Tên hoặc mã nhà mạng (Viettel, VinaPhone, MobiFone...)" }
                    },
                    required = new[] { "carrierName" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetPrefixInfo",
                description = "Tra cứu thông tin, điểm uy tín và ý nghĩa phong thủy của một đầu số điện thoại (ví dụ: 098, 090, 091, 088, 086...).",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        prefixValue = new { type = "string", description = "Đầu số cần tra cứu (ví dụ: 098, 090, 091, 086, 088...)" }
                    },
                    required = new[] { "prefixValue" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "GetAuctionDetail",
                description = "Lấy thông tin chi tiết một phiên đấu giá SIM VIP theo mã phiên (AuctionCode) bao gồm giá hiện tại, bước giá, tiền đặt cọc và thời hạn.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        auctionCode = new { type = "string", description = "Mã phiên đấu giá (ví dụ: AUC123456)" }
                    },
                    required = new[] { "auctionCode" }
                }
            }
        }
    };


    #endregion
}
