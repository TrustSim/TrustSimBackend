using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class SimValuationMathEngine : ISimValuationMathEngine
{
    private const decimal StandardBasePrice = 200000m; // 200,000 VND

    public SimMathValuationResult CalculateBaselinePrice(SimExtractedFeatures features)
    {
        var p0 = StandardBasePrice;
        var prefixFactor = features.PrefixReputationScore;
        var rhythmFactor = features.RhythmBaseFactor;

        // Tính tổng điểm đóng góp của các danh mục
        // CategoryFactor = 1 + sum((Multiplier - 1) * Weight)
        float categoryContributionSum = 0f;
        foreach (var cat in features.DetectedCategories)
        {
            if (cat.Multiplier > 1.0f)
            {
                categoryContributionSum += (cat.Multiplier - 1.0f) * cat.ScoreContribution;
            }
        }
        var categoryFactor = 1.0f + categoryContributionSum;

        // Tính hệ số phong thủy
        float fengShuiFactor = 1.0f;
        if (features.HasAvoidNumbers)
        {
            fengShuiFactor *= 0.7f; // Phạt -30% nếu dính 49, 53
        }
        if (features.HasLuckyEnd)
        {
            fengShuiFactor *= 1.15f; // Thưởng +15% nếu đuôi số vượng khí 8, 9, 68, 79
        }

        // Áp dụng công thức cơ sở
        decimal calculatedPrice = p0 * (decimal)prefixFactor * (decimal)rhythmFactor * (decimal)categoryFactor * (decimal)fengShuiFactor;

        // Phân cấp thị trường thực tế (5 Phân khúc thị trường SIM Việt Nam tham chiếu Sim Thăng Long & Chợ Sim 24h)
        decimal tierFloorPrice = 0m;
        foreach (var cat in features.DetectedCategories)
        {
            switch (cat.CategoryCode)
            {
                case "BAT_QUY":
                case "CUU_QUY":
                    tierFloorPrice = Math.Max(tierFloorPrice, 15_000_000_000m);
                    break;
                case "LUC_QUY":
                    tierFloorPrice = Math.Max(tierFloorPrice, 4_500_000_000m);
                    break;
                case "NGU_QUY_VIP_DAU_CO":
                    tierFloorPrice = Math.Max(tierFloorPrice, 1_500_000_000m);
                    break;
                case "NGU_QUY":
                    tierFloorPrice = Math.Max(tierFloorPrice, features.PrefixReputationScore >= 1.3f ? 450_000_000m : 250_000_000m);
                    break;
                case "DAI_TAXI_86":
                case "DAI_TAXI_68":
                    tierFloorPrice = Math.Max(tierFloorPrice, 1_200_000_000m);
                    break;
                case "SANH_RONG":
                    tierFloorPrice = Math.Max(tierFloorPrice, 800_000_000m);
                    break;
                case "TU_QUY_VIP_DAU_CO":
                    tierFloorPrice = Math.Max(tierFloorPrice, 350_000_000m);
                    break;
                case "TU_QUY":
                    tierFloorPrice = Math.Max(tierFloorPrice, features.PrefixReputationScore >= 1.3f ? 120_000_000m : 60_000_000m);
                    break;
                case "SANH_TIEN_6789":
                    tierFloorPrice = Math.Max(tierFloorPrice, 80_000_000m);
                    break;
            }
        }

        if (tierFloorPrice > 0m)
        {
            calculatedPrice = Math.Max(calculatedPrice, tierFloorPrice);
        }
        else if (features.DetectedCategories.Count == 0 || features.DetectedCategories.All(c => c.Multiplier <= 1.5f))
        {
            // Phổ thông / số phụ: giữ mức giá hợp lý dưới 800.000 VNĐ
            calculatedPrice = Math.Min(calculatedPrice, 450_000m);
        }

        // Làm tròn giá tiền theo chuẩn thị trường
        calculatedPrice = RoundToCommercialPrice(calculatedPrice);

        var formulaExplanation = $"P_base = {p0:N0} đ (Gốc) × {prefixFactor:F2} (Đầu số {features.Prefix}) × {rhythmFactor:F1} ({features.DetectedRhythmName}) × {categoryFactor:F2} (Danh mục) × {fengShuiFactor:F2} (Phong thủy) = {calculatedPrice:N0} đ";

        return new SimMathValuationResult
        {
            BaselinePrice = calculatedPrice,
            BasePriceStandard = p0,
            PrefixFactor = prefixFactor,
            RhythmFactor = rhythmFactor,
            CategoryFactor = categoryFactor,
            FengShuiFactor = fengShuiFactor,
            FormulaExplanation = formulaExplanation
        };
    }

    private static decimal RoundToCommercialPrice(decimal price)
    {
        if (price < 1000000m)
        {
            // Dưới 1 triệu: làm tròn đến 10,000 đ
            return Math.Round(price / 10000m, MidpointRounding.AwayFromZero) * 10000m;
        }
        if (price < 10000000m)
        {
            // 1 triệu đến 10 triệu: làm tròn đến 50,000 đ
            return Math.Round(price / 50000m, MidpointRounding.AwayFromZero) * 50000m;
        }
        if (price < 100000000m)
        {
            // 10 triệu đến 100 triệu: làm tròn đến 500,000 đ
            return Math.Round(price / 500000m, MidpointRounding.AwayFromZero) * 500000m;
        }
        // Trên 100 triệu: làm tròn đến 1,000,000 đ
        return Math.Round(price / 1000000m, MidpointRounding.AwayFromZero) * 1000000m;
    }
}
