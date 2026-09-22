using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu khung giá thị trường viễn thông thực tế và định mức tham chiếu theo thể loại SIM.
/// </summary>
public class SimMarketHistoryPlugin
{
    private readonly ISimCategoryRepository _categoryRepo;

    public SimMarketHistoryPlugin(ISimCategoryRepository categoryRepo)
    {
        _categoryRepo = categoryRepo;
    }

    [KernelFunction, Description("Tra cứu khung giá thị trường tham chiếu, khoảng giá giao dịch thực tế và biên độ thanh khoản theo thể loại số SIM (Tứ quý, Tam hoa, Thần tài, Lộc phát, Sảnh tiến...) và đầu số.")]
    public async Task<string> GetMarketPriceComparablesAsync(
        [Description("Mã thể loại SIM (ví dụ: TU_QUY, TAM_HOA, THAN_TAI, LOC_PHAT, SANH_TIEN, TAXI, NGU_QUY, LUC_QUY)")] string? categoryCode = null,
        [Description("Đầu số nhà mạng (ví dụ: 098, 090, 091, 088, 086, 070...)")] string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var allCategories = await _categoryRepo.GetAllAsync(cancellationToken);
        
        var matchedCategory = !string.IsNullOrWhiteSpace(categoryCode)
            ? allCategories.FirstOrDefault(c => string.Equals(c.CategoryCode, categoryCode, StringComparison.OrdinalIgnoreCase)
                                             || c.CategoryName.Contains(categoryCode, StringComparison.OrdinalIgnoreCase))
            : null;

        var priceBenchmark = GetCategoryPriceBenchmark(matchedCategory?.CategoryCode ?? categoryCode ?? "GENERAL", prefix);

        return JsonSerializer.Serialize(new
        {
            categoryCode = matchedCategory?.CategoryCode ?? categoryCode ?? "ALL",
            categoryName = matchedCategory?.CategoryName ?? "Tổng hợp thị trường",
            prefixFilter = prefix ?? "Tất cả đầu số",
            multiplier = matchedCategory?.ValuationMultiplier ?? 1.0f,
            marketTier = priceBenchmark.TierName,
            priceRanges = new
            {
                retailShowroom = priceBenchmark.RetailRange,
                marketP2P = priceBenchmark.P2PRange,
                dealerBuyback24h = priceBenchmark.DealerRange
            },
            liquidityLevel = priceBenchmark.Liquidity,
            marketTrendAdvice = priceBenchmark.TrendAdvice
        });
    }

    [KernelFunction, Description("Lấy danh sách các thể loại SIM số đẹp phổ biến trên sàn TrustSim kèm hệ số nhân giá trị.")]
    public async Task<string> GetSimCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepo.GetAllAsync(cancellationToken);
        if (categories.Count == 0)
        {
            return "{\"total\":0,\"items\":[]}";
        }

        return JsonSerializer.Serialize(new
        {
            total = categories.Count,
            items = categories.Select(c => new
            {
                code = c.CategoryCode,
                name = c.CategoryName,
                multiplier = c.ValuationMultiplier,
                description = c.Description
            })
        });
    }


    private static (string TierName, string RetailRange, string P2PRange, string DealerRange, string Liquidity, string TrendAdvice) GetCategoryPriceBenchmark(string categoryCode, string? prefix)
    {
        string normalized = (categoryCode ?? string.Empty).ToUpperInvariant();
        bool isVintagePrefix = prefix != null && (prefix.StartsWith("098") || prefix.StartsWith("090") || prefix.StartsWith("091"));

        if (normalized.Contains("LUC_QUY") || normalized.Contains("LỤC QUÝ"))
        {
            return ("Tầng 4 - Siêu VIP Cực Phẩm", "4.000.000.000 - 15.000.000.000 VND", "3.000.000.000 - 10.000.000.000 VND", "2.000.000.000 - 6.000.000.000 VND", "Thanh khoản cao trong giới Doanh nhân", "Tài sản lưu trữ giá trị, biên độ tăng giá 15-25%/năm.");
        }

        if (normalized.Contains("NGU_QUY") || normalized.Contains("NGŨ QUÝ"))
        {
            if (isVintagePrefix)
            {
                return ("Tầng 4 - Siêu VIP Đầu Cổ", "1.500.000.000 - 2.500.000.000 VND", "1.200.000.000 - 1.800.000.000 VND", "900.000.000 - 1.200.000.000 VND", "Rất cao", "Đầu cổ 098/090/091 giữ giá cực tốt, được săn đón bởi lãnh đạo cấp cao.");
            }
            return ("Tầng 4 - Siêu VIP Đầu Mới", "300.000.000 - 800.000.000 VND", "250.000.000 - 600.000.000 VND", "180.000.000 - 450.000.000 VND", "Khá cao", "Phù hợp doanh nghiệp làm Hotline tổng đài thương hiệu.");
        }

        if (normalized.Contains("TU_QUY") || normalized.Contains("TỨ QUÝ"))
        {
            if (isVintagePrefix)
            {
                return ("Tầng 3 - Hotline Doanh nhân VIP", "150.000.000 - 500.000.000 VND", "120.000.000 - 380.000.000 VND", "80.000.000 - 250.000.000 VND", "Rất nhanh (24h - 48h)", "Tứ quý 6, 8, 9 đầu 09 luôn khan hiếm và có nhu cầu mua cao nhất thị trường.");
            }
            return ("Tầng 3 - VIP Tầm Trung", "40.000.000 - 150.000.000 VND", "30.000.000 - 120.000.000 VND", "20.000.000 - 80.000.000 VND", "Nhanh", "Tứ quý 1, 2, 3, 4, 7 hoặc đầu số mới 03, 07, 08.");
        }

        if (normalized.Contains("SANH_TIEN") || normalized.Contains("SẢNH TIẾN") || normalized.Contains("RỒNG"))
        {
            return ("Tầng 3 - Tiến Đẳng Cấp (6789 / Sảnh Rồng)", "80.000.000 - 450.000.000 VND", "60.000.000 - 350.000.000 VND", "45.000.000 - 250.000.000 VND", "Cao", "Sảnh tiến 6789 (San bằng tất cả) mang ý nghĩa thăng tiến bền vững trong sự nghiệp.");
        }

        if (normalized.Contains("TAM_HOA") || normalized.Contains("TAM HOA"))
        {
            return ("Tầng 2 - Số Đẹp Tầm Trung", "3.000.000 - 50.000.000 VND", "2.500.000 - 40.000.000 VND", "1.500.000 - 25.000.000 VND", "Rất cao", "Dòng sim đại chúng phổ biến nhất, thanh khoản tức thì.");
        }

        if (normalized.Contains("LOC_PHAT") || normalized.Contains("LỘC PHÁT") || normalized.Contains("THAN_TAI") || normalized.Contains("THẦN TÀI"))
        {
            return ("Tầng 2 - Tài Lộc Kinh Doanh", "2.000.000 - 80.000.000 VND", "1.500.000 - 65.000.000 VND", "1.000.000 - 40.000.000 VND", "Rất cao", "Các cặp đuôi 6868, 8686, 7979 được giới kinh doanh buôn bán cực kỳ ưa chuộng.");
        }

        return ("Tầng 1-2 - Thị Trường Chung", "500.000 - 20.000.000 VND", "400.000 - 15.000.000 VND", "250.000 - 10.000.000 VND", "Trung bình", "Tùy thuộc vào nhà mạng, độ tròn trịa và sự hài hòa âm dương của dãy số.");
    }
}
