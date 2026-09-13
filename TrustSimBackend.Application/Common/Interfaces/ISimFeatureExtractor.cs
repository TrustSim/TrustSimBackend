using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ bóc tách đặc trưng số học, đầu số, nhịp số và danh mục phong thủy của SIM (Tầng 1)
/// </summary>
public interface ISimFeatureExtractor
{
    /// <summary>
    /// Phân tích toàn diện chuỗi số SIM để trích xuất các đặc tính số học
    /// </summary>
    SimExtractedFeatures ExtractFeatures(string phoneNumber, string? carrierName = null, float prefixScore = 1.0f);
}
