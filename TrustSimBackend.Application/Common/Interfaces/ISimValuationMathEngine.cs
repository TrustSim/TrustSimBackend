using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Mô hình toán học tính giá sàn cơ sở của SIM (Tầng 2)
/// </summary>
public interface ISimValuationMathEngine
{
    /// <summary>
    /// Tính toán giá sàn cơ sở toán học từ các đặc trưng số học đã bóc tách
    /// </summary>
    SimMathValuationResult CalculateBaselinePrice(SimExtractedFeatures features);
}
