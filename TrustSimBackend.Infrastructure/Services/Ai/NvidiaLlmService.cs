using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Dịch vụ kết nối NVIDIA NIM AI Models (LLaMA 3.3 70B / Nemotron / DeepSeek)
/// Hỗ trợ Agentic Tool/Function Calling độc lập để tự động gọi Repository & RAG Engine.
/// </summary>
public class NvidiaLlmService : INvidiaLlmService
{
    private readonly HttpClient _httpClient;
    private readonly NvidiaSettings _settings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NvidiaLlmService> _logger;

    public NvidiaLlmService(
        HttpClient httpClient,
        IOptions<NvidiaSettings> settings,
        IServiceScopeFactory scopeFactory,
        ILogger<NvidiaLlmService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<string> GenerateCompletionAsync(
        string systemPrompt,
        string userPrompt,
        float temperature = 0.3f,
        int maxTokens = 2048,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Contains("YOUR_") || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Nvidia API Key chưa được cấu hình hoặc không hợp lệ. Sử dụng phản hồi AI cục bộ.");
            return "Chào bạn! Đây là hệ thống AI Chuyên gia Tư vấn Sàn Giao dịch SIM Số đẹp TrustSim. Tôi luôn sẵn sàng hỗ trợ bạn lựa chọn số SIM hoàn hảo nhất theo ngân sách và phong thủy.";
        }

        try
        {
            var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/chat/completions";
            var requestPayload = new
            {
                model = string.IsNullOrWhiteSpace(_settings.LlmModel) ? "deepseek-ai/deepseek-v4-flash-0731" : _settings.LlmModel,
                messages = new object[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                },
                temperature = temperature,
                max_tokens = maxTokens
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");

            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("NVIDIA LLM API thất bại ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return string.Empty;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<NvidiaChatCompletionResponse>(responseJson);

            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi/Timeout khi gọi NVIDIA LLM API: {Message}", ex.Message);
            return string.Empty;
        }
    }

    public async Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default)
    {
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

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var systemPrompt = @"Bạn là Chủ tịch Hội đồng Thẩm định & Định giá SIM Viễn thông Độc lập tại Sàn Giao dịch TrustSim Việt Nam.
Nhiệm vụ của bạn: Kết hợp đa chiều giữa:
1. Phân tích thế nhịp ngắt tối ưu và các tổ hợp Hán - Việt bộc lộ.
2. Giá sàn cơ sở toán học tính được từ thuật toán nội bộ.
3. Tài liệu nghiên cứu thị trường RAG cung cấp.
4. TRI THỨC MỞ RỘNG VỀ THỊ TRƯỜNG SIM VIỆT NAM (Thực tế giao dịch các dòng Ngũ quý, Lục quý, Taxi, Thần tài, Lộc phát; độ khan hiếm và uy tín của các đầu số cổ 090, 098, 091; phong thủy Cửu Tinh Đồ Pháp, Bát Trạch Tam Nguyên và giá trị định vị thương hiệu Hotline VIP cho doanh nghiệp).

Hãy đưa ra kết luận định giá khách quan, sát với mặt bằng giá giao dịch thực tế trên thị trường tự do, khoảng giá thanh lý nhanh (MinPrice), giá giao dịch tối ưu (SuggestedPrice), giá kỳ vọng tối đa (MaxPrice), hệ số trượt giá thị trường (MarketMultiplier từ 0.8 đến 2.5) và bài phân tích giải trình thuyết phục 4 phần bằng tiếng Việt.

Bạn BẮT BUỘC phải trả về kết quả thuần JSON tuân thủ cấu trúc sau (không kèm markdown mở đầu/kết thúc):
{
  ""suggestedPrice"": 2200000000,
  ""minPrice"": 1850000000,
  ""maxPrice"": 2500000000,
  ""confidenceScore"": 0.98,
  ""marketMultiplier"": 1.22,
  ""rhythmRecommendation"": ""2.2.2.2.2"",
  ""formattedNumber"": ""09.86.86.86.86"",
  ""categoryCodes"": [""DAI_TAXI_86"", ""LOC_PHAT_86"", ""KHONG_THAT_BAT""],
  ""valuationReasoning"": ""Bài phân tích chi tiết gồm 4 phần: (1) Thế số ngắt nhịp và âm luật phát âm, (2) Ý nghĩa các tổ hợp tài lộc bộc lộ, (3) Ý nghĩa phong thủy & bản mệnh, (4) So sánh đối chiếu thị trường viễn thông thực tế và định vị thương hiệu Hotline..."",
  ""fengShuiSummary"": ""Tóm tắt ngắn gọn ý nghĩa may mắn tài lộc...""
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

        var categoriesText = string.Join(", ", features.DetectedCategories.Select(c => $"{c.CategoryName} ({c.CategoryCode}, hệ số {c.Multiplier}x)"));

        var userPrompt = $@"THÔNG TIN SỐ SIM & KHẢO SÁT THẾ NHỊP TỐI ƯU:
- Số điện thoại gốc: {features.CleanPhoneNumber} (Định dạng hiển thị: {features.RawPhoneNumber})
- Nhà mạng: {features.CarrierName} (Đầu số: {features.Prefix}, Điểm uy tín đầu số: {features.PrefixReputationScore})
- Thế nhịp tối ưu lựa chọn: {features.SelectedRhythmValue} (Hiển thị ngắt nhịp: {features.FormattedNumber})
- Tên thế số: {features.DetectedRhythmName} (Hệ số nhịp: {features.RhythmBaseFactor}x)
- So sánh với nhịp khác: {features.AlternativeRhythmsComparison}
- Các tổ hợp Hán - Việt bộc lộ: {categoriesText}
- Ghi chú phong thủy: {features.FengShuiNotes}
- Giá cơ sở toán học tính được: {mathResult.BaselinePrice:N0} VND
- Công thức giải trình: {mathResult.FormulaExplanation}

TRI THỨC THỊ TRƯỜNG TỪ RAG (TRÍCH DẪN TÀI LIỆU NỘI BỘ):
{ragContextBuilder}

YÊU CẦU THẨM ĐỊNH MỞ RỘNG:
Hãy phân tích cặn kẽ tại sao cách ngắt nhịp {features.FormattedNumber} lại bộc lộ thế số đỉnh cao. Hãy kết hợp tri thức thị trường viễn thông ngoài đời thực, phong thủy số học và tài liệu RAG để đưa ra mức giá khách quan, chuẩn xác nhất và xuất JSON kết quả.";

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey) && !_settings.ApiKey.Contains("YOUR_") && _settings.ApiKey.Length >= 10)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(18));

                _logger.LogInformation("[NVIDIA LLM CALL START] Bắt đầu gọi model {Model} thẩm định giá số SIM {Phone}...", _settings.LlmModel, features.CleanPhoneNumber);

                var rawLlmText = await GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.2f, maxTokens: 600, cts.Token);
                var cleanedJson = ExtractJsonString(rawLlmText);
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

                    parsed.EngineSource = "NVIDIA_CLOUD_LLM";
                    parsed.ExecutionTimeMs = sw.ElapsedMilliseconds;

                    _logger.LogInformation("[NVIDIA LLM CALL SUCCESS] Thẩm định thành công số {Phone} trong {ElapsedMs}ms! Giá đề xuất: {Price:N0} VNĐ (Nguồn: {Source})",
                        features.CleanPhoneNumber, sw.ElapsedMilliseconds, parsed.SuggestedPrice, parsed.EngineSource);

                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NVIDIA LLM FALLBACK TRIGGERED] Không thể parse JSON từ Cloud LLM hoặc quá thời gian ({ElapsedMs}ms): {Msg}. Kích hoạt Bộ Định Giá Cục Bộ 4 Tầng.", sw.ElapsedMilliseconds, ex.Message);
            }
        }

        var fallback = GenerateFallbackValuation(features, mathResult, effectiveChunks);
        fallback.EngineSource = "LOCAL_FALLBACK_ENGINE";
        fallback.ExecutionTimeMs = sw.ElapsedMilliseconds;
        _logger.LogInformation("[NVIDIA LLM FALLBACK] Đã kích hoạt Bộ Định Giá Cục Bộ 4 Tầng cho số {Phone} sau {ElapsedMs}ms.", features.CleanPhoneNumber, sw.ElapsedMilliseconds);
        return fallback;
    }

    private static readonly object[] ConsultantTools = new object[]
    {
        new
        {
            type = "function",
            function = new
            {
                name = "query_fengshui_and_market_knowledge",
                description = "Truy vấn kho tri thức RAG phong thủy ngũ hành, ý nghĩa các con số và cẩm nang định giá viễn thông. CHỈ gọi khi người dùng hỏi về kiến thức phong thủy, hợp mệnh, ý nghĩa đuôi số hoặc quy tắc chọn số.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "Câu hỏi hoặc từ khóa tìm kiếm tri thức phong thủy" }
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
                name = "search_available_sims",
                description = "Tìm kiếm danh sách số SIM thực tế đang có sẵn trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số. CHỈ gọi khi người dùng muốn tìm số SIM cụ thể hoặc cần gợi ý danh sách số có thể mua.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        minPrice = new { type = "number", description = "Giá tối thiểu (VND)" },
                        maxPrice = new { type = "number", description = "Giá tối đa (VND)" },
                        carrier = new { type = "string", description = "Nhà mạng (Viettel, VinaPhone, MobiFone...)" },
                        prefix = new { type = "string", description = "Đầu số (098, 090, 091, 088...)" },
                        keyword = new { type = "string", description = "Mẫu số đuôi hoặc chuỗi lặp (ví dụ: 68, 79, 8686...)" }
                    }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "extract_and_inspect_sim_cadence",
                description = "Bóc tách thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể khi người dùng hỏi về một số điện thoại nhất định (ví dụ: 0986868686).",
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

    private async Task<(string ResultJson, string Summary)> ExecuteAgenticToolAsync(
        string fnName,
        string fnArgs,
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        List<ConsultationSimSuggestion> collectedSims,
        List<Guid> collectedChunkIds,
        List<string> referencedDocs,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ragService = scope.ServiceProvider.GetService<IRagRetrievalService>();
        var simRepo = scope.ServiceProvider.GetService<ISimNumberRepository>();
        var featureExtractor = scope.ServiceProvider.GetService<ISimFeatureExtractor>();

        try
        {
            var argsObj = JsonSerializer.Deserialize<JsonElement>(string.IsNullOrWhiteSpace(fnArgs) ? "{}" : fnArgs);

            if (fnName == "query_fengshui_and_market_knowledge")
            {
                var query = argsObj.TryGetProperty("query", out var q) ? q.GetString() : userMessage;
                if (ragService != null && !string.IsNullOrWhiteSpace(query))
                {
                    var chunks = await ragService.RetrieveRelevantChunksAsync(query, topK: 3, minSimilarity: 0.3, cancellationToken);
                    foreach (var c in chunks)
                    {
                        if (!collectedChunkIds.Contains(c.ChunkId)) collectedChunkIds.Add(c.ChunkId);
                        if (!referencedDocs.Contains(c.DocumentName)) referencedDocs.Add(c.DocumentName);
                    }
                    var json = JsonSerializer.Serialize(chunks.Select(c => new { document = c.DocumentName, content = c.Content, similarity = c.Similarity }));
                    return (json, $"Tra cứu thành công {chunks.Count} tài liệu phong thủy RAG");
                }
            }
            else if (fnName == "search_available_sims")
            {
                decimal? minP = argsObj.TryGetProperty("minPrice", out var minVal) && minVal.TryGetDecimal(out var d1) ? d1 : budgetMin;
                decimal? maxP = argsObj.TryGetProperty("maxPrice", out var maxVal) && maxVal.TryGetDecimal(out var d2) ? d2 : budgetMax;
                string? carrier = argsObj.TryGetProperty("carrier", out var cVal) ? cVal.GetString() : null;
                string? prefix = argsObj.TryGetProperty("prefix", out var pVal) ? pVal.GetString() : null;
                string? keyword = argsObj.TryGetProperty("keyword", out var kVal) ? kVal.GetString() : null;

                if (simRepo != null)
                {
                    var dbSims = await simRepo.GetAvailableSimsForConsultationAsync(minP, maxP, carrier, prefix, keyword, limit: 6, cancellationToken);
                    var items = dbSims.Select(s => new ConsultationSimSuggestion
                    {
                        SimNumberId = s.Id,
                        PhoneNumber = s.PhoneNumber,
                        Price = s.Price,
                        CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                        HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
                    }).ToList();

                    foreach (var item in items)
                    {
                        if (!collectedSims.Any(x => x.SimNumberId == item.SimNumberId))
                        {
                            collectedSims.Add(item);
                        }
                    }
                    var json = JsonSerializer.Serialize(items);
                    return (json, $"Tìm thấy {items.Count} số SIM thực tế đang mở bán trên sàn");
                }
            }
            else if (fnName == "extract_and_inspect_sim_cadence")
            {
                var phone = argsObj.TryGetProperty("phoneNumber", out var pn) ? pn.GetString() : userMessage;
                if (featureExtractor != null && !string.IsNullOrWhiteSpace(phone))
                {
                    var feat = featureExtractor.ExtractFeatures(phone);
                    var json = JsonSerializer.Serialize(new
                    {
                        raw = feat.RawPhoneNumber,
                        formatted = feat.FormattedNumber,
                        rhythm = feat.DetectedRhythmName,
                        rhythmFactor = feat.RhythmBaseFactor,
                        categories = feat.DetectedCategories.Select(c => c.CategoryName),
                        fengShuiNotes = feat.FengShuiNotes,
                        comparison = feat.AlternativeRhythmsComparison
                    });
                    return (json, $"Đã bóc tách thế nhịp: {feat.FormattedNumber} ({feat.DetectedRhythmName})");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi thực thi Tool {FnName}: {Msg}", fnName, ex.Message);
            return ($"{{\"error\":\"Lỗi khi gọi tool {fnName}\"}}", "Lỗi thực thi tool");
        }

        return ("{}", "Hoàn tất tra cứu");
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
        var collectedSims = new List<ConsultationSimSuggestion>();
        var collectedChunkIds = new List<Guid>();
        var referencedDocs = new List<string>();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Contains("YOUR_") || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Nvidia API Key chưa được cấu hình. Sử dụng Agentic Fallback Dispatcher.");
            return await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);
        }

        try
        {
            var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/chat/completions";
            var modelName = string.IsNullOrWhiteSpace(_settings.LlmModel) ? "deepseek-ai/deepseek-v4-flash-0731" : _settings.LlmModel;

            var systemPrompt = $@"Bạn là Chuyên gia Cao cấp Tư vấn SIM Số đẹp & Phong thủy tại Sàn Giao dịch TrustSim Việt Nam.
Bạn có quyền truy cập vào 3 công cụ độc lập:
1. `query_fengshui_and_market_knowledge`: Tra cứu tri thức phong thủy ngũ hành, ý nghĩa các con số, cặp số cát tường và quy chuẩn thị trường.
2. `search_available_sims`: Tra cứu kho SIM thực tế đang mở bán trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số.
3. `extract_and_inspect_sim_cadence`: Phân tích chi tiết thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể khi người dùng hỏi về số đó.

NGUYÊN TẮC HOẠT ĐỘNG AGENTIC:
- CHỈ gọi công cụ khi THỰC SỰ CẦN THIẾT để có đủ dữ liệu trả lời người dùng. Không gọi công cụ dư thừa.
- Nếu người dùng chỉ chào hỏi hoặc hỏi câu chung chung, hãy trả lời trực tiếp mà không gọi tool.
- Khi người dùng muốn tìm SIM mua hoặc hỏi số phù hợp, BẮT BUỘC gọi tool `search_available_sims` để lấy các số SIM thực tế đang có trong kho sàn TrustSim.
- Khi tư vấn phong thủy theo bản mệnh, có thể gọi `query_fengshui_and_market_knowledge` nếu cần tra cứu sâu.
- Thông tin khách hàng đã cung cấp: Năm sinh: {userBirthYear?.ToString() ?? "Chưa rõ"}, Nghề nghiệp: {userCareer ?? "Chưa rõ"}, Mệnh ngũ hành: {fengShuiElement ?? "Chưa rõ"}, Ngân sách: {(budgetMin.HasValue ? budgetMin.Value.ToString("N0") : "0")} - {(budgetMax.HasValue ? budgetMax.Value.ToString("N0") : "Không giới hạn")} VND.";

            var messages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:\n{conversationHistory}\n\nCÂU HỎI MỚI CỦA KHÁCH HÀNG:\n\"{userMessage}\"" }
            };

            int maxIterations = 4;
            int iteration = 0;

            while (iteration < maxIterations)
            {
                iteration++;

                var requestPayload = new
                {
                    model = modelName,
                    messages = messages,
                    tools = ConsultantTools,
                    tool_choice = "auto",
                    temperature = 0.2f,
                    max_tokens = 350
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
                request.Content = jsonContent;

                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("NVIDIA NIM Tool Calling API error ({StatusCode}): {Err}", response.StatusCode, err);
                    break;
                }

                var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
                var completionResult = JsonSerializer.Deserialize<NvidiaChatCompletionResponse>(responseJson);
                var choice = completionResult?.Choices?.FirstOrDefault();

                if (choice?.Message == null) break;

                // Kiểm tra nếu LLM muốn gọi Tools
                if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
                {
                    messages.Add(new
                    {
                        role = "assistant",
                        content = choice.Message.Content,
                        tool_calls = choice.Message.ToolCalls.Select(tc => new
                        {
                            id = tc.Id,
                            type = tc.Type,
                            function = new
                            {
                                name = tc.Function.Name,
                                arguments = tc.Function.Arguments
                            }
                        }).ToList()
                    });

                    foreach (var tc in choice.Message.ToolCalls)
                    {
                        var (resultJson, _) = await ExecuteAgenticToolAsync(
                            tc.Function.Name,
                            tc.Function.Arguments,
                            userMessage,
                            budgetMin,
                            budgetMax,
                            collectedSims,
                            collectedChunkIds,
                            referencedDocs,
                            cancellationToken);

                        messages.Add(new
                        {
                            role = "tool",
                            tool_call_id = tc.Id,
                            name = tc.Function.Name,
                            content = resultJson
                        });
                    }

                    continue;
                }

                if (!string.IsNullOrWhiteSpace(choice.Message.Content))
                {
                    return new AiConsultationLlmResponse
                    {
                        ReplyContent = choice.Message.Content,
                        SuggestedSims = collectedSims,
                        ReferencedChunkIds = collectedChunkIds,
                        ReferencedDocuments = referencedDocs,
                        TokensUsed = (int)(choice.Message.Content.Length / 3.5)
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi trong ReAct Tool Calling Loop: {Message}", ex.Message);
        }

        return await ExecuteFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken);
    }

    private async Task<NvidiaChatCompletionResponse?> CallNvidiaChatAsync(
        object requestPayload,
        string requestUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var jsonContent = new StringContent(JsonSerializer.Serialize(requestPayload), Encoding.UTF8, "application/json");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            request.Content = jsonContent;

            var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<NvidiaChatCompletionResponse>(responseJson);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi khi gọi NVIDIA NIM API: {Message}", ex.Message);
            return null;
        }
    }

    private async IAsyncEnumerable<string> ReadStreamDeltasAsync(
        object streamPayload,
        string requestUrl,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        HttpResponseMessage? streamRes = null;
        try
        {
            var streamContent = new StringContent(JsonSerializer.Serialize(streamPayload), Encoding.UTF8, "application/json");
            using var streamReq = new HttpRequestMessage(HttpMethod.Post, requestUrl);
            streamReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey.Trim());
            streamReq.Content = streamContent;

            streamRes = await _httpClient.SendAsync(streamReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi kết nối Stream SSE: {Message}", ex.Message);
            yield break;
        }

        if (streamRes != null && streamRes.IsSuccessStatusCode)
        {
            using var stream = await streamRes.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                string? line = null;
                try
                {
                    line = await reader.ReadLineAsync(cancellationToken);
                }
                catch
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("data: "))
                {
                    var data = line.Substring(6).Trim();
                    if (data == "[DONE]") break;

                    string? deltaText = null;
                    try
                    {
                        var chunk = JsonSerializer.Deserialize<NvidiaStreamChunkResponse>(data);
                        deltaText = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrEmpty(deltaText))
                    {
                        yield return deltaText;
                    }
                }
            }
        }
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
        var collectedSims = new List<ConsultationSimSuggestion>();
        var collectedChunkIds = new List<Guid>();
        var referencedDocs = new List<string>();

        if (string.IsNullOrWhiteSpace(_settings.ApiKey) || _settings.ApiKey.Contains("YOUR_") || _settings.ApiKey.Length < 10)
        {
            _logger.LogWarning("Nvidia API Key chưa được cấu hình. Sử dụng Stream Fallback Dispatcher.");
            await foreach (var evt in StreamFallbackConsultationAgentAsync(userMessage, budgetMin, budgetMax, cancellationToken))
            {
                yield return evt;
            }
            yield break;
        }

        var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/chat/completions";
        var modelName = string.IsNullOrWhiteSpace(_settings.LlmModel) ? "deepseek-ai/deepseek-v4-flash-0731" : _settings.LlmModel;

        var systemPrompt = $@"Bạn là Chuyên gia Cao cấp Tư vấn SIM Số đẹp & Phong thủy tại Sàn Giao dịch TrustSim Việt Nam.
Bạn có quyền truy cập vào 3 công cụ độc lập:
1. `query_fengshui_and_market_knowledge`: Tra cứu tri thức phong thủy ngũ hành, ý nghĩa các con số, cặp số cát tường và quy chuẩn thị trường.
2. `search_available_sims`: Tra cứu kho SIM thực tế đang mở bán trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số.
3. `extract_and_inspect_sim_cadence`: Phân tích chi tiết thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể khi người dùng hỏi về số đó.

NGUYÊN TẮC HOẠT ĐỘNG AGENTIC:
- CHỈ gọi công cụ khi THỰC SỰ CẦN THIẾT để có đủ dữ liệu trả lời người dùng. Không gọi công cụ dư thừa.
- Nếu người dùng chỉ chào hỏi hoặc hỏi câu chung chung, hãy trả lời trực tiếp mà không gọi tool.
- Khi người dùng muốn tìm SIM mua hoặc hỏi số phù hợp, BẮT BUỘC gọi tool `search_available_sims` để lấy các số SIM thực tế đang có trong kho sàn TrustSim.
- Khi tư vấn phong thủy theo bản mệnh, có thể gọi `query_fengshui_and_market_knowledge` nếu cần tra cứu sâu.
- Thông tin khách hàng đã cung cấp: Năm sinh: {userBirthYear?.ToString() ?? "Chưa rõ"}, Nghề nghiệp: {userCareer ?? "Chưa rõ"}, Mệnh ngũ hành: {fengShuiElement ?? "Chưa rõ"}, Ngân sách: {(budgetMin.HasValue ? budgetMin.Value.ToString("N0") : "0")} - {(budgetMax.HasValue ? budgetMax.Value.ToString("N0") : "Không giới hạn")} VND.";

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = $"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:\n{conversationHistory}\n\nCÂU HỎI MỚI CỦA KHÁCH HÀNG:\n\"{userMessage}\"" }
        };

        int maxIterations = 2;
        int iteration = 0;
        bool readyForStreamAnswer = false;
        bool hasStreamedTokens = false;

        while (iteration < maxIterations)
        {
            iteration++;

            var requestPayload = new
            {
                model = modelName,
                messages = messages,
                tools = iteration == 1 ? ConsultantTools : null,
                tool_choice = iteration == 1 ? "auto" : "none",
                temperature = 0.2f,
                max_tokens = 250
            };

            NvidiaChatCompletionResponse? completionResult = null;
            try
            {
                using var callCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                callCts.CancelAfter(TimeSpan.FromSeconds(12));
                completionResult = await CallNvidiaChatAsync(requestPayload, requestUrl, callCts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[NVIDIA LLM TOOL DECISION] Không thể gọi Cloud LLM phân tích Tool: {Msg}", ex.Message);
                break;
            }

            var choice = completionResult?.Choices?.FirstOrDefault();
            if (choice?.Message == null) break;

            if (choice.Message.ToolCalls != null && choice.Message.ToolCalls.Count > 0)
            {
                foreach (var tc in choice.Message.ToolCalls)
                {
                    _logger.LogInformation("[AGENTIC TOOL START] Kích hoạt tool {ToolName}...", tc.Function.Name);

                    yield return new ChatConsultationStreamEvent
                    {
                        EventType = "tool_start",
                        ToolName = tc.Function.Name,
                        ToolSummary = $"Đang kích hoạt công cụ {tc.Function.Name}..."
                    };

                    var (resultJson, summary) = await ExecuteAgenticToolAsync(
                        tc.Function.Name,
                        tc.Function.Arguments,
                        userMessage,
                        budgetMin,
                        budgetMax,
                        collectedSims,
                        collectedChunkIds,
                        referencedDocs,
                        cancellationToken);

                    _logger.LogInformation("[AGENTIC TOOL END] Tool {ToolName} hoàn tất: {Summary}", tc.Function.Name, summary);

                    yield return new ChatConsultationStreamEvent
                    {
                        EventType = "tool_end",
                        ToolName = tc.Function.Name,
                        ToolSummary = summary
                    };
                }

                readyForStreamAnswer = true;
                break;
            }

            readyForStreamAnswer = true;
            break;
        }

        if (readyForStreamAnswer)
        {
            var streamMessages = new List<object>
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:\n{conversationHistory}\n\nCÂU HỎI MỚI CỦA KHÁCH HÀNG:\n\"{userMessage}\"" }
            };

            if (collectedSims.Count > 0 || referencedDocs.Count > 0)
            {
                var sbTools = new StringBuilder("DỮ LIỆU ĐÃ TRA CỨU ĐƯỢC TỪ HỆ THỐNG:\n");
                if (collectedSims.Count > 0)
                {
                    sbTools.AppendLine($"- Danh sách SIM đang có trên sàn TrustSim: {string.Join(", ", collectedSims.Select(s => $"{s.PhoneNumber} (Giá {s.Price:N0}đ, {s.HighlightReason})"))}");
                }
                if (referencedDocs.Count > 0)
                {
                    sbTools.AppendLine($"- Tri thức phong thủy RAG: {string.Join("; ", referencedDocs)}");
                }
                sbTools.AppendLine("Dựa trên các dữ liệu trên, hãy viết câu trả lời trực tiếp, hấp dẫn và súc tích bằng tiếng Việt cho khách hàng.");
                streamMessages.Add(new { role = "system", content = sbTools.ToString() });
            }

            var streamPayload = new
            {
                model = modelName,
                messages = streamMessages,
                stream = true,
                temperature = 0.4f,
                max_tokens = 600
            };

            using var streamCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            streamCts.CancelAfter(TimeSpan.FromSeconds(25));

            await foreach (var delta in ReadStreamDeltasAsync(streamPayload, requestUrl, streamCts.Token))
            {
                hasStreamedTokens = true;
                yield return new ChatConsultationStreamEvent
                {
                    EventType = "token",
                    Delta = delta
                };
            }
        }

        if (!hasStreamedTokens)
        {
            _logger.LogInformation("[NVIDIA LLM FALLBACK] Kích hoạt Stream Fallback Dispatcher sau {ElapsedMs}ms", sw.ElapsedMilliseconds);

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
            ReferencedChunkIds = collectedChunkIds,
            ReferencedDocuments = referencedDocs,
            EngineSource = hasStreamedTokens ? "NVIDIA_CLOUD_LLM" : "LOCAL_FALLBACK_ENGINE",
            ExecutionTimeMs = sw.ElapsedMilliseconds
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
                ToolName = "search_available_sims",
                ToolSummary = "Đang quét kho SIM trên sàn TrustSim..."
            };
            await Task.Delay(40, cancellationToken);
            yield return new ChatConsultationStreamEvent
            {
                EventType = "tool_end",
                ToolName = "search_available_sims",
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
            await Task.Delay(20, cancellationToken);
        }

        yield return new ChatConsultationStreamEvent
        {
            EventType = "done",
            SuggestedSims = fallbackRes.SuggestedSims,
            ReferencedChunkIds = fallbackRes.ReferencedChunkIds,
            ReferencedDocuments = fallbackRes.ReferencedDocuments
        };
    }

    private async Task<AiConsultationLlmResponse> ExecuteFallbackConsultationAgentAsync(
        string userMessage,
        decimal? budgetMin,
        decimal? budgetMax,
        CancellationToken cancellationToken)
    {
        var collectedSims = new List<ConsultationSimSuggestion>();
        var collectedChunkIds = new List<Guid>();
        var referencedDocs = new List<string>();

        using var scope = _scopeFactory.CreateScope();
        var simRepo = scope.ServiceProvider.GetService<ISimNumberRepository>();
        var ragService = scope.ServiceProvider.GetService<IRagRetrievalService>();

        var lower = userMessage.ToLowerInvariant();
        bool needsSimSearch = lower.Contains("sim") || lower.Contains("mua") || lower.Contains("tìm") || lower.Contains("số") || lower.Contains("giá") || budgetMax.HasValue;
        bool needsRag = lower.Contains("mệnh") || lower.Contains("phong thủy") || lower.Contains("tuổi") || lower.Contains("năm sinh") || lower.Contains("hợp");

        if (needsSimSearch && simRepo != null)
        {
            var dbSims = await simRepo.GetAvailableSimsForConsultationAsync(budgetMin, budgetMax, limit: 6, cancellationToken: cancellationToken);
            collectedSims = dbSims.Select(s => new ConsultationSimSuggestion
            {
                SimNumberId = s.Id,
                PhoneNumber = s.PhoneNumber,
                Price = s.Price,
                CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
            }).ToList();
        }

        if (needsRag && ragService != null)
        {
            var chunks = await ragService.RetrieveRelevantChunksAsync(userMessage, topK: 3, minSimilarity: 0.3, cancellationToken);
            foreach (var c in chunks)
            {
                collectedChunkIds.Add(c.ChunkId);
                if (!referencedDocs.Contains(c.DocumentName)) referencedDocs.Add(c.DocumentName);
            }
        }

        var reply = GenerateFallbackConsultationText(userMessage, collectedSims);

        return new AiConsultationLlmResponse
        {
            ReplyContent = reply,
            SuggestedSims = collectedSims,
            ReferencedChunkIds = collectedChunkIds,
            ReferencedDocuments = referencedDocs,
            TokensUsed = (int)(reply.Length / 3.5)
        };
    }

    public async Task<string> GenerateConsultationResponseAsync(
        string userMessage,
        string conversationHistory,
        IReadOnlyList<DocumentChunkSearchResult> ragFengShuiChunks,
        IReadOnlyList<ConsultationSimSuggestion> availableSims,
        CancellationToken cancellationToken = default)
    {
        var result = await GenerateConsultationWithAgenticToolsAsync(
            userMessage,
            conversationHistory,
            cancellationToken: cancellationToken);

        return result.ReplyContent;
    }

    private static AiValuationLlmResponse GenerateFallbackValuation(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult>? ragChunks)
    {
        var basePrice = mathResult.BaselinePrice;
        decimal minPrice = Math.Round(basePrice * 0.85m / 100000m, MidpointRounding.AwayFromZero) * 100000m;
        decimal maxPrice = Math.Round(basePrice * 1.18m / 100000m, MidpointRounding.AwayFromZero) * 100000m;
        if (minPrice < 100000m) minPrice = 100000m;

        var sbReasoning = new StringBuilder();
        sbReasoning.AppendLine($"Số thuê bao **{features.CleanPhoneNumber}** khi được định dạng theo nhịp **{features.FormattedNumber}** ({features.DetectedRhythmName}) bộc lộ trọn vẹn nét đẹp và giá trị thẩm mỹ vượt trội:");
        sbReasoning.AppendLine($"1. **Thế số & Âm luật Ngắt nhịp**: Nhịp ngắt `{features.FormattedNumber}` (mã `{features.SelectedRhythmValue}`) giúp phát âm dứt khoát, uy lực, tạo nhịp điệu dồn dập và nâng cao tính nhận diện thương hiệu tuyệt đối cho các doanh nghiệp làm Hotline VIP.");
        
        if (!string.IsNullOrEmpty(features.AlternativeRhythmsComparison))
        {
            sbReasoning.AppendLine($"2. **So sánh Thế số**: {features.AlternativeRhythmsComparison}");
        }

        if (features.DetectedCategories.Count > 0)
        {
            var catNames = string.Join(", ", features.DetectedCategories.Select(c => $"{c.CategoryName} ({c.Multiplier}x)"));
            sbReasoning.AppendLine($"3. **Tổ hợp Hán - Việt & Phong thủy**: Số chứa các cụm tài lộc đắt giá: *{catNames}*. {features.FengShuiNotes}");
        }

        sbReasoning.AppendLine($"4. **Đối chiếu Bối cảnh Thị trường**: Kết hợp đầu số cổ uy tín **{features.Prefix}** ({features.CarrierName}), số SIM có biên độ thanh khoản thực tế trong khoảng **{minPrice:N0} đ** đến **{maxPrice:N0} đ**. Mức giá đề xuất tối ưu đưa lên sàn TrustSim là **{basePrice:N0} đ**.");

        return new AiValuationLlmResponse
        {
            SuggestedPrice = basePrice,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            ConfidenceScore = 0.96f,
            MarketMultiplier = 1.10f,
            RhythmRecommendation = features.SelectedRhythmValue,
            FormattedNumber = features.FormattedNumber,
            CategoryCodes = features.DetectedCategories.Select(c => c.CategoryCode).ToList(),
            ValuationReasoning = sbReasoning.ToString().Trim(),
            FengShuiSummary = string.IsNullOrEmpty(features.FengShuiNotes) 
                ? "Thế số kích hoạt vượng khí tối đa, số sạch mang lại hanh thông và tài lộc dồi dào cho gia chủ kinh doanh." 
                : features.FengShuiNotes
        };
    }

    private static string GenerateFallbackConsultationText(string userMessage, IReadOnlyList<ConsultationSimSuggestion>? availableSims)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        sb.AppendLine("Dựa trên thông tin và tiêu chí tìm kiếm của bạn, tôi xin gợi ý các định hướng chọn số mang lại vượng khí tốt nhất:");
        sb.AppendLine("- Nên ưu tiên các đầu số cổ uy tín của các nhà mạng lớn (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) để khẳng định vị thế và uy tín cá nhân.");
        sb.AppendLine("- Kết hợp các đuôi số phong thủy kích hoạt tài lộc như Thần tài (39, 79), Lộc phát (68, 86) hoặc số tiến (6789, 1368).");

        if (availableSims != null && availableSims.Count > 0)
        {
            sb.AppendLine("\nTôi đã rà soát kho SIM thực tế trên sàn TrustSim và tuyển chọn các số đẹp phù hợp nhất dưới đây để bạn tham khảo:");
            foreach (var sim in availableSims)
            {
                sb.AppendLine($"• **{sim.PhoneNumber}** ({sim.CarrierName}) - Giá: **{sim.Price:N0} đ** : {sim.HighlightReason}");
            }
        }

        return sb.ToString();
    }

    private static string ExtractJsonString(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "{}";
        var trimmed = text.Trim();

        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            return trimmed;
        }

        var startIdx = trimmed.IndexOf('{');
        var endIdx = trimmed.LastIndexOf('}');

        if (startIdx >= 0 && endIdx > startIdx)
        {
            return trimmed.Substring(startIdx, endIdx - startIdx + 1);
        }

        return "{}";
    }

    private class NvidiaChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<NvidiaChoiceItem> Choices { get; set; } = new();
    }

    private class NvidiaChoiceItem
    {
        [JsonPropertyName("message")]
        public NvidiaMessageItem? Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class NvidiaMessageItem
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "assistant";

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("tool_calls")]
        public List<NvidiaToolCall>? ToolCalls { get; set; }
    }

    private class NvidiaStreamChunkResponse
    {
        [JsonPropertyName("choices")]
        public List<NvidiaStreamChoiceItem>? Choices { get; set; }
    }

    private class NvidiaStreamChoiceItem
    {
        [JsonPropertyName("delta")]
        public NvidiaStreamDeltaItem? Delta { get; set; }

        [JsonPropertyName("finish_reason")]
        public string? FinishReason { get; set; }
    }

    private class NvidiaStreamDeltaItem
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }
    }
}
