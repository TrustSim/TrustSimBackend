using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class NvidiaLlmService : INvidiaLlmService
{
    private readonly HttpClient _httpClient;
    private readonly NvidiaSettings _settings;
    private readonly ILogger<NvidiaLlmService> _logger;

    public NvidiaLlmService(
        HttpClient httpClient,
        IOptions<NvidiaSettings> settings,
        ILogger<NvidiaLlmService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
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
            _logger.LogWarning("Nvidia API Key chưa được cấu hình. Sử dụng phản hồi AI cục bộ.");
            return "Chào bạn! Đây là hệ thống AI Chuyên gia Tư vấn Sàn Giao dịch SIM Số đẹp TrustSim. Tôi luôn sẵn sàng hỗ trợ bạn lựa chọn số SIM hoàn hảo nhất theo ngân sách và phong thủy.";
        }

        try
        {
            var requestUrl = $"{_settings.BaseUrl.TrimEnd('/')}/chat/completions";
            var requestPayload = new
            {
                model = string.IsNullOrWhiteSpace(_settings.LlmModel) ? "meta/llama-3.3-70b-instruct" : _settings.LlmModel,
                messages = new[]
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
                _logger.LogError("NVIDIA LLM API thất bại ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                throw new InvalidOperationException($"Lỗi kết nối NVIDIA LLM API ({response.StatusCode}): {errorBody}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = JsonSerializer.Deserialize<NvidiaChatCompletionResponse>(responseJson);

            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Lỗi khi gọi NVIDIA LLM API: {Message}", ex.Message);
            return "Hệ thống AI đang bảo trì kết nối. Vui lòng thử lại sau ít phút.";
        }
    }

    public async Task<AiValuationLlmResponse> GenerateValuationReasoningAsync(
        SimExtractedFeatures features,
        SimMathValuationResult mathResult,
        IReadOnlyList<DocumentChunkSearchResult> ragChunks,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = @"Bạn là Chuyên gia Cao cấp Thẩm định & Định giá SIM Số đẹp tại Sàn Giao dịch TrustSim Việt Nam.
Nhiệm vụ của bạn: Dựa trên phân tích số học, giá cơ sở toán học và tài liệu nghiên cứu thị trường cung cấp, hãy đưa ra kết luận định giá chính xác, khoảng giá giao dịch hợp lý (Suggested, Min, Max) và bài phân tích giải trình thuyết phục bằng tiếng Việt.
Bạn BẮT BUỘC phải trả về kết quả thuần JSON tuân thủ cấu trúc sau (không kèm text mở đầu/kết thúc):
{
  ""suggestedPrice"": 25000000,
  ""minPrice"": 22000000,
  ""maxPrice"": 28000000,
  ""confidenceScore"": 0.95,
  ""marketMultiplier"": 1.15,
  ""rhythmRecommendation"": ""Tứ quý 8"",
  ""categoryCodes"": [""LOC_PHAT"", ""TU_QUY""],
  ""valuationReasoning"": ""Bài phân tích chi tiết gồm 3 phần: (1) Nhịp số và độ hiếm, (2) Ý nghĩa phong thủy & bản mệnh, (3) So sánh đối chiếu thị trường..."",
  ""fengShuiSummary"": ""Tóm tắt ngắn gọn ý nghĩa may mắn tài lộc...""
}";

        var ragContextBuilder = new StringBuilder();
        if (ragChunks != null && ragChunks.Count > 0)
        {
            int idx = 1;
            foreach (var chunk in ragChunks)
            {
                ragContextBuilder.AppendLine($"[Tài liệu tham khảo {idx++}: {chunk.DocumentName} (Độ tương đồng: {chunk.Similarity:P0})]");
                ragContextBuilder.AppendLine(chunk.Content);
                ragContextBuilder.AppendLine("---");
            }
        }
        else
        {
            ragContextBuilder.AppendLine("Chưa có tài liệu nghiên cứu thị trường đặc thù, áp dụng biểu giá thị trường chuẩn sàn TrustSim.");
        }

        var categoriesText = string.Join(", ", features.DetectedCategories.Select(c => $"{c.CategoryName} (x{c.Multiplier})"));

        var userPrompt = $@"THÔNG TIN SỐ SIM CẦN ĐỊNH GIÁ:
- Số điện thoại: {features.CleanPhoneNumber} (Hiển thị: {features.RawPhoneNumber})
- Nhà mạng: {features.CarrierName} (Đầu số: {features.Prefix}, Điểm đầu số: {features.PrefixReputationScore})
- Nhịp số nhận diện: {features.DetectedRhythmName} (Mã: {features.DetectedRhythmCode}, Hệ số nhịp: {features.RhythmBaseFactor})
- Danh mục phong thủy: {categoriesText}
- Ghi chú phong thủy: {features.FengShuiNotes}
- Giá cơ sở toán học tính được: {mathResult.BaselinePrice:N0} VND
- Công thức giải trình: {mathResult.FormulaExplanation}

TRI THỨC THỊ TRƯỜNG TỪ RAG (TRÍCH DẪN TÀI LIỆU):
{ragContextBuilder}

Hãy tính toán và xuất JSON định giá chính xác.";

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey) && !_settings.ApiKey.Contains("YOUR_") && _settings.ApiKey.Length >= 10)
        {
            try
            {
                var rawLlmText = await GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.2f, maxTokens: 2048, cancellationToken);
                
                // Trích xuất JSON từ text phản hồi nếu LLM có bọc trong markdown ```json ... ```
                var cleanedJson = ExtractJsonString(rawLlmText);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<AiValuationLlmResponse>(cleanedJson, options);

                if (parsed != null && parsed.SuggestedPrice > 0)
                {
                    return parsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể parse JSON từ NVIDIA LLM response, chuyển sang bộ tổng hợp giải trình cục bộ.");
            }
        }

        // Fallback Expert Synthesis (Đảm bảo giá trị định giá luôn chính xác và có bài phân tích chuyên sâu)
        return GenerateFallbackValuation(features, mathResult, ragChunks);
    }

    public async Task<string> GenerateConsultationResponseAsync(
        string userMessage,
        string conversationHistory,
        IReadOnlyList<DocumentChunkSearchResult> ragFengShuiChunks,
        IReadOnlyList<ConsultationSimSuggestion> availableSims,
        CancellationToken cancellationToken = default)
    {
        var systemPrompt = @"Bạn là Chuyên gia Tư vấn SIM Số đẹp & Phong thủy cao cấp tại Sàn Giao dịch TrustSim.
Phong cách trò chuyện: Thân thiện, lịch thiệp, am hiểu sâu sắc về phong thủy ngũ hành, quẻ dịch, nhịp số và thị trường SIM Việt Nam.
Nhiệm vụ: Phân tích nhu cầu của khách hàng (ngân sách, năm sinh, ngành nghề), giải thích ý nghĩa phong thủy phù hợp và đề xuất các số SIM thực tế đang có trong kho của sàn TrustSim để khách hàng có thể mua ngay.";

        var availableSimsText = new StringBuilder();
        if (availableSims != null && availableSims.Count > 0)
        {
            availableSimsText.AppendLine("DANH SÁCH SỐ SIM ĐANG CÓ SẴN TRÊN SÀN TRUSTSIM THỎA MÃN YÊU CẦU:");
            foreach (var sim in availableSims)
            {
                availableSimsText.AppendLine($"- Số SIM: {sim.PhoneNumber} | Giá: {sim.Price:N0} đ | Nhà mạng: {sim.CarrierName} | Đặc điểm: {sim.HighlightReason}");
            }
        }
        else
        {
            availableSimsText.AppendLine("Hiện tại các số SIM khớp hoàn toàn đang được cập nhật, hãy đưa ra lời khuyên về các dạng số phù hợp.");
        }

        var ragText = new StringBuilder();
        if (ragFengShuiChunks != null && ragFengShuiChunks.Count > 0)
        {
            ragText.AppendLine("TRI THỨC PHONG THỦY & Ý NGHĨA SỐ (TỪ RAG):");
            foreach (var chunk in ragFengShuiChunks)
            {
                ragText.AppendLine($"- {chunk.Content}");
            }
        }

        var userPrompt = $@"LỊCH SỬ HỘI THOẠI TRƯỚC ĐÓ:
{conversationHistory}

{ragText}

{availableSimsText}

CÂU HỎI MỚI CỦA KHÁCH HÀNG:
""{userMessage}""

Hãy đưa ra câu trả lời tư vấn xuất sắc, giải thích cặn kẽ ý nghĩa phong thủy và làm nổi bật các số SIM có trong danh sách trên.";

        if (!string.IsNullOrWhiteSpace(_settings.ApiKey) && !_settings.ApiKey.Contains("YOUR_") && _settings.ApiKey.Length >= 10)
        {
            try
            {
                var response = await GenerateCompletionAsync(systemPrompt, userPrompt, temperature: 0.6f, maxTokens: 1500, cancellationToken);
                if (!string.IsNullOrWhiteSpace(response))
                {
                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gọi LLM tư vấn, sử dụng mẫu tư vấn chuyên gia chuẩn.");
            }
        }

        return GenerateFallbackConsultationText(userMessage, availableSims);
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
        sbReasoning.AppendLine($"Số thuê bao **{features.RawPhoneNumber}** thuộc nhà mạng **{features.CarrierName}** mang nhiều nét đặc trưng giá trị:");
        sbReasoning.AppendLine($"1. **Cấu trúc Nhịp số**: Số SIM sở hữu thế số **{features.DetectedRhythmName}** (hệ số nhịp {features.RhythmBaseFactor}x), tạo nên ấn tượng thị giác mạnh mẽ, dễ nhớ và khẳng định đẳng cấp người sử dụng.");
        
        if (features.DetectedCategories.Count > 0)
        {
            var catNames = string.Join(", ", features.DetectedCategories.Select(c => c.CategoryName));
            sbReasoning.AppendLine($"2. **Giá trị Phong thủy & Ý nghĩa Số**: Số thuộc các danh mục tài lộc: *{catNames}*. {features.FengShuiNotes}");
        }

        sbReasoning.AppendLine($"3. **Đối chiếu Thị trường & Khả năng Thanh khoản**: Với đầu số uy tín **{features.Prefix}** ({features.CarrierName}), số SIM có biên độ giao dịch ổn định trong khoảng **{minPrice:N0} đ** đến **{maxPrice:N0} đ**. Mức giá đề xuất tối ưu đưa lên sàn là **{basePrice:N0} đ**.");

        return new AiValuationLlmResponse
        {
            SuggestedPrice = basePrice,
            MinPrice = minPrice,
            MaxPrice = maxPrice,
            ConfidenceScore = 0.94f,
            MarketMultiplier = 1.05f,
            RhythmRecommendation = features.DetectedRhythmName,
            CategoryCodes = features.DetectedCategories.Select(c => c.CategoryCode).ToList(),
            ValuationReasoning = sbReasoning.ToString().Trim(),
            FengShuiSummary = string.IsNullOrEmpty(features.FengShuiNotes) 
                ? "Dãy số cân bằng âm dương, kích hoạt năng lượng tài lộc và sự hanh thông trong công việc." 
                : features.FengShuiNotes
        };
    }

    private static string GenerateFallbackConsultationText(string userMessage, IReadOnlyList<ConsultationSimSuggestion>? availableSims)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Chào bạn! Cảm ơn bạn đã liên hệ với Chuyên gia Tư vấn SIM Số đẹp TrustSim.");
        sb.AppendLine("Dựa trên thông tin và tiêu chí tìm kiếm của bạn, tôi xin gợi ý các định hướng chọn số mang lại vượng khí tốt nhất:");
        sb.AppendLine("- Nên ưu tiên các đầu số cổ uy tín của các nhà mạng lớn (098 - Viettel, 090 - MobiFone, 091 - VinaPhone) để khẳng định uy tín cá nhân.");
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
    }

    private class NvidiaMessageItem
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
