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

        // 1. Gọi Gemini với tools
        var (firstChoice, _) = await CallOpenAiEndpointAsync(
            GetPreferredModel(),
            messages,
            includeTools: true,
            maxTokens: 1500,
            temperature: 0.2f,
            cancellationToken: cancellationToken);

        if (firstChoice.ToolCalls != null && firstChoice.ToolCalls.Count > 0)
        {
            messages.Add(firstChoice);

            foreach (var tc in firstChoice.ToolCalls)
            {
                _logger.LogInformation("[NATIVE TOOL CALL] Function: {Func}, Args: {Args}", tc.Function.Name, tc.Function.Arguments);

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

            // Tổng hợp câu trả lời từ Gemini
            var (finalChoice, _) = await CallOpenAiEndpointAsync(
                GetPreferredModel(),
                messages,
                includeTools: false,
                maxTokens: 1500,
                temperature: 0.3f,
                cancellationToken: cancellationToken);

            result.ReplyContent = CleanLlmResponse(finalChoice.Content ?? string.Empty);
        }
        else
        {
            result.ReplyContent = CleanLlmResponse(firstChoice.Content ?? string.Empty);
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
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Gemini API Key chưa được cấu hình. Sử dụng Agentic Fallback Dispatcher.");
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
                int limit = arguments.TryGetProperty("limit", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetInt32() : 5;
                var res = await plugin.GetActiveAuctionsAsync(limit, cancellationToken);
                return (res, "Đã tra cứu danh sách phiên đấu giá SIM VIP trực tiếp trên sàn", new List<ConsultationSimSuggestion>());
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

            default:
                return ("{\"message\":\"Tool không tồn tại\"}", $"Thực thi công cụ {toolName}", new List<ConsultationSimSuggestion>());
        }
    }

    #endregion

    #region 4. Helper Methods, Prompt Building & REST Client

    private string GetPreferredModel()
    {
        return !string.IsNullOrWhiteSpace(_settings.ChatModel) ? _settings.ChatModel : "gemini-3.1-flash-lite";
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

NGUYÊN TẮC TƯ VẤN QUAN TRỌNG:
1. Khi khách hàng muốn tìm SIM, hỏi có số nào theo nhà mạng (Viettel, Vina, Mobi...), đầu số, đuôi số (6868, 7979, tam hoa, tứ quý...) hoặc mức giá, hãy GỌI FUNCTION 'SearchAvailableSims' với các tham số tương ứng để lấy dữ liệu thực tế từ kho TrustSim.
2. Khi khách chỉ hỏi kiến thức lý thuyết phong thủy (như 'tôi sinh năm 2004 mệnh gì, hợp số mấy'), hãy GỌI FUNCTION 'GetFengShuiGuidelineByBirthYear' hoặc 'QueryFengShuiKnowledge' và tập trung trả lời rõ ràng câu hỏi đó trước.
3. Khi khách hỏi về các phiên đấu giá, hãy GỌI FUNCTION 'GetActiveAuctions'.
4. Trả lời với giọng văn niềm nở, thanh lịch, chuyên nghiệp và uy tín của chuyên gia viễn thông.
5. Thông tin khách hàng đã biết: Năm sinh: {userBirthYear?.ToString() ?? "Chưa rõ"}, Nghề nghiệp: {userCareer ?? "Chưa rõ"}, Mệnh: {fengShuiElement ?? "Chưa rõ"}, Ngân sách: {(budgetMin.HasValue ? budgetMin.Value.ToString("N0") : "0")} - {(budgetMax.HasValue ? budgetMax.Value.ToString("N0") : "Không giới hạn")} VND.";
    }

    private async Task<string> ExecuteChatWithCandidateModelsAsync(
        List<OpenAiMessage> messages,
        bool includeTools,
        int maxTokens,
        CancellationToken cancellationToken)
    {
        var configuredModel = GetPreferredModel();
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
                var (choice, _) = await CallOpenAiEndpointAsync(model, messages, includeTools, maxTokens, temperature: 0.2f, cancellationToken);
                if (!string.IsNullOrWhiteSpace(choice.Content))
                {
                    return CleanLlmResponse(choice.Content);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[LLM CANDIDATE FAILED] Model {Model} thất bại: {Msg}", model, ex.Message);
            }
        }

        return string.Empty;
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

        // Kiểm tra số lượng ngoặc nhọn mở và đóng
        int openBraces = json.Count(c => c == '{');
        int closeBraces = json.Count(c => c == '}');

        // Nếu thiếu ngoặc kép chưa đóng
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

    private async Task<AiConsultationLlmResponse> ExecuteFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var simRepo = scope.ServiceProvider.GetService<ISimNumberRepository>();
        var sims = simRepo != null
            ? await simRepo.GetAvailableSimsForConsultationAsync(budgetMin, budgetMax, null, null, null, limit: 6, cancellationToken)
            : new List<TrustSimBackend.Domain.Entities.Sim.SimNumber>();

        var suggestions = sims.Select(s => new ConsultationSimSuggestion
        {
            SimNumberId = s.Id,
            PhoneNumber = s.PhoneNumber,
            Price = s.Price,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
            HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
        }).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        sb.AppendLine("Dựa trên thông tin và tiêu chí tìm kiếm của bạn, tôi xin gợi ý các định hướng chọn số mang lại vượng khí tốt nhất:");
        sb.AppendLine("- Nên ưu tiên các đầu số cổ uy tín của các nhà mạng lớn (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) để khẳng định vị thế và uy tín cá nhân.");
        sb.AppendLine("- Kết hợp các đuôi số phong thủy kích hoạt tài lộc như Thần tài (39, 79), Lộc phát (68, 86) hoặc số tiến (6789, 1368).");

        if (suggestions.Count > 0)
        {
            sb.AppendLine("\nTôi đã rà soát kho SIM thực tế trên sàn TrustSim và tuyển chọn các số đẹp phù hợp nhất dưới đây để bạn tham khảo:");
            foreach (var sim in suggestions)
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
            SuggestedSims = suggestions,
            ReferencedChunkIds = new List<Guid>(),
            ReferencedDocuments = new List<string>(),
            TokensUsed = 120,
            EngineSource = "LOCAL_FALLBACK_ENGINE"
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

    #endregion

    #region 5. Data Models & Tool Definitions

    private class OpenAiMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<OpenAiToolCall>? ToolCalls { get; set; }

        [JsonPropertyName("tool_call_id")]
        public string? ToolCallId { get; set; }
    }

    private class OpenAiToolCall
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "function";

        [JsonPropertyName("function")]
        public OpenAiFunctionCall Function { get; set; } = new();
    }

    private class OpenAiFunctionCall
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("arguments")]
        public string Arguments { get; set; } = "{}";
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
                description = "Lấy danh sách các phiên đấu giá SIM số đẹp VIP đang diễn ra trực tiếp trên sàn TrustSim. Gọi khi khách hỏi về đấu giá.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        limit = new { type = "integer", description = "Số lượng phiên đấu giá cần lấy (mặc định: 5)" }
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
        }
    };

    #endregion
}
