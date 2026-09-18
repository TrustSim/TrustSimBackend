using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin bóc tách thế nhịp ngắt và phân tích cấu trúc tổ hợp số học.
/// </summary>
public class SimCadencePlugin
{
    private readonly ISimFeatureExtractor _featureExtractor;

    public SimCadencePlugin(ISimFeatureExtractor featureExtractor)
    {
        _featureExtractor = featureExtractor;
    }

    [KernelFunction, Description("Bóc tách thế nhịp ngắt và các tổ hợp phong thủy của một số SIM cụ thể khi người dùng hỏi về một số điện thoại nhất định (ví dụ: 0986868686).")]
    public string AnalyzeSimCadence(
        [Description("Số điện thoại cần bóc tách phân tích")] string phoneNumber)
    {
        var feat = _featureExtractor.ExtractFeatures(phoneNumber);
        return JsonSerializer.Serialize(new
        {
            raw = feat.RawPhoneNumber,
            formatted = feat.FormattedNumber,
            rhythm = feat.DetectedRhythmName,
            rhythmFactor = feat.RhythmBaseFactor,
            categories = feat.DetectedCategories.Select(c => c.CategoryName),
            fengShuiNotes = feat.FengShuiNotes,
            comparison = feat.AlternativeRhythmsComparison
        });
    }
}
