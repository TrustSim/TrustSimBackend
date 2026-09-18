using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu tri thức phong thủy ngũ hành, can chi và cẩm nang số học viễn thông qua RAG.
/// </summary>
public class FengShuiKnowledgePlugin
{
    private readonly IRagRetrievalService _ragService;

    public FengShuiKnowledgePlugin(IRagRetrievalService ragService)
    {
        _ragService = ragService;
    }

    [KernelFunction, Description("Truy vấn kho tri thức RAG phong thủy ngũ hành, ý nghĩa các con số và cẩm nang định giá viễn thông. CHỈ gọi khi người dùng hỏi về kiến thức phong thủy, hợp mệnh, ý nghĩa đuôi số hoặc quy tắc chọn số.")]
    public async Task<string> QueryFengShuiKnowledgeAsync(
        [Description("Câu hỏi hoặc từ khóa tìm kiếm tri thức phong thủy")] string query,
        CancellationToken cancellationToken = default)
    {
        var chunks = await _ragService.RetrieveRelevantChunksAsync(query, topK: 3, minSimilarity: 0.3, cancellationToken);
        if (chunks.Count == 0)
        {
            return "{\"total\":0,\"items\":[],\"message\":\"Không tìm thấy tài liệu phù hợp.\"}";
        }

        return JsonSerializer.Serialize(new
        {
            total = chunks.Count,
            items = chunks.Select(c => new
            {
                chunkId = c.ChunkId,
                document = c.DocumentName,
                content = c.Content,
                similarity = c.Similarity
            })
        });
    }

    [KernelFunction, Description("Tra cứu bản mệnh ngũ hành, quái số và các cặp số cát tường tương sinh theo năm sinh.")]
    public string GetFengShuiGuidelineByBirthYear(
        [Description("Năm sinh dương lịch của khách hàng (ví dụ: 1990, 1988)")] int birthYear)
    {
        var canChi = CalculateCanChi(birthYear);
        var (element, favorable, unfavorable, advice) = CalculateFengShuiProfile(birthYear, canChi);

        return JsonSerializer.Serialize(new
        {
            birthYear = birthYear,
            canChi = canChi,
            element = element,
            favorableNumbers = favorable,
            unfavorableNumbers = unfavorable,
            advice = advice
        });
    }

    private static string CalculateCanChi(int year)
    {
        string[] cans = { "Canh", "Tân", "Nhâm", "Quý", "Giáp", "Ất", "Bính", "Đinh", "Mậu", "Kỷ" };
        string[] chis = { "Thân", "Dậu", "Tuất", "Hợi", "Tý", "Sửu", "Dần", "Mão", "Thìn", "Tỵ", "Ngọ", "Mùi" };
        return $"{cans[year % 10]} {chis[year % 12]}";
    }

    private static (string Element, int[] Favorable, int[] Unfavorable, string Advice) CalculateFengShuiProfile(int year, string canChi)
    {
        int canVal = (year % 10) switch { 4 or 5 => 1, 6 or 7 => 2, 8 or 9 => 3, 0 or 1 => 4, _ => 5 };
        int chiVal = (year % 12) switch { 4 or 5 or 10 or 11 => 0, 0 or 1 or 6 or 7 => 1, _ => 2 };
        int menhVal = canVal + chiVal;
        if (menhVal > 5) menhVal -= 5;

        return menhVal switch
        {
            1 => ("Kim", new[] { 2, 5, 6, 7, 8 }, new[] { 9 }, $"Gia chủ sinh năm {year} ({canChi}) mệnh Kim (Thổ sinh Kim). Nên chọn các đuôi số 6, 7 (bản mệnh) hoặc 2, 5, 8 (hành Thổ tương sinh). Tránh số 9 (Hỏa khắc Kim)."),
            2 => ("Thủy", new[] { 1, 6, 7 }, new[] { 2, 5, 8 }, $"Gia chủ sinh năm {year} ({canChi}) mệnh Thủy (Kim sinh Thủy). Nên chọn các đuôi số 1 (bản mệnh) hoặc 6, 7 (hành Kim tương sinh). Tránh số 2, 5, 8 (Thổ khắc Thủy)."),
            3 => ("Hỏa", new[] { 3, 4, 9 }, new[] { 1 }, $"Gia chủ sinh năm {year} ({canChi}) mệnh Hỏa (Mộc sinh Hỏa). Nên chọn các đuôi số 9 (bản mệnh) hoặc 3, 4 (hành Mộc tương sinh). Tránh số 1 (Thủy khắc Hỏa)."),
            4 => ("Thổ", new[] { 0, 2, 5, 8, 9 }, new[] { 3, 4 }, $"Gia chủ sinh năm {year} ({canChi}) mệnh Thổ (Hỏa sinh Thổ). Nên chọn các đuôi số 2, 5, 8 (bản mệnh) hoặc 9 (hành Hỏa tương sinh, cực vượng phát). Tránh số 3, 4 (Mộc khắc Thổ)."),
            _ => ("Mộc", new[] { 1, 3, 4 }, new[] { 6, 7 }, $"Gia chủ sinh năm {year} ({canChi}) mệnh Mộc (Thủy sinh Mộc). Nên chọn các đuôi số 3, 4 (bản mệnh) hoặc 1 (hành Thủy tương sinh). Tránh số 6, 7 (Kim khắc Mộc).")
        };
    }
}
