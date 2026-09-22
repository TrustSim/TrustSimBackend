using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin định giá nhanh và bóc tách cấu trúc giá trị của số SIM viễn thông.
/// Kết hợp mô hình bóc tách đặc trưng số học (Tầng 1) và công thức toán học cơ sở (Tầng 2).
/// </summary>
public class SimValuationPlugin
{
    private readonly ISimFeatureExtractor _featureExtractor;
    private readonly ISimValuationMathEngine _mathEngine;

    public SimValuationPlugin(
        ISimFeatureExtractor featureExtractor,
        ISimValuationMathEngine mathEngine)
    {
        _featureExtractor = featureExtractor;
        _mathEngine = mathEngine;
    }

    [KernelFunction, Description("Định giá nhanh và phân tích cấu trúc giá trị của một số điện thoại bất kỳ. Trả về giá sàn cơ sở toán học, phân khúc giá thị trường (Đại lý, P2P, Thợ thu), thế nhịp ngắt và điểm tổng nút phong thủy.")]
    public string EvaluateSimPrice(
        [Description("Số điện thoại cần thẩm định giá (ví dụ: 0988888888, 0903123456, 0869192776)")] string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return "{\"error\":\"Vui lòng cung cấp số điện thoại hợp lệ để định giá.\"}";
        }

        var features = _featureExtractor.ExtractFeatures(phoneNumber);
        var mathResult = _mathEngine.CalculateBaselinePrice(features);

        decimal basePrice = mathResult.BaselinePrice;
        decimal retailPrice = Math.Round(basePrice * 1.25m / 100000) * 100000;
        decimal p2pPrice = Math.Round(basePrice / 100000) * 100000;
        decimal dealerBuybackPrice = Math.Round(basePrice * 0.70m / 100000) * 100000;

        return JsonSerializer.Serialize(new
        {
            phoneNumber = features.CleanPhoneNumber,
            formattedNumber = features.FormattedNumber,
            carrier = features.CarrierName,
            prefix = features.Prefix,
            prefixGroup = features.PrefixGroup,
            middleGroup = features.MiddleGroup,
            tailGroup = features.TailGroup,
            totalPoints = features.TotalPoints,
            rhythm = new
            {
                code = features.DetectedRhythmCode,
                name = features.DetectedRhythmName,
                pattern = features.SelectedRhythmValue,
                factor = features.RhythmBaseFactor
            },
            categories = features.DetectedCategories.Select(c => new
            {
                code = c.CategoryCode,
                name = c.CategoryName,
                multiplier = c.Multiplier,
                description = c.Description
            }),
            baselinePrice = basePrice,
            priceEstimates = new
            {
                retailPrice = retailPrice,
                marketP2PPrice = p2pPrice,
                dealerBuybackPrice = dealerBuybackPrice,
                currency = "VND"
            },
            factors = new
            {
                prefixFactor = mathResult.PrefixFactor,
                rhythmFactor = mathResult.RhythmFactor,
                categoryFactor = mathResult.CategoryFactor,
                fengShuiFactor = mathResult.FengShuiFactor
            },
            fengShuiNotes = features.FengShuiNotes,
            rhythmComparison = features.AlternativeRhythmsComparison,
            formulaExplanation = mathResult.FormulaExplanation
        });
    }
}
