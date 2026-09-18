using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin giải đáp chính sách giao dịch đảm bảo (Escrow), giữ tiền an toàn và tính toán biểu phí sàn/đặt cọc.
/// </summary>
public class TradeAndEscrowPlugin
{
    [KernelFunction, Description("Giải thích quy trình giao dịch đảm bảo (Escrow), giữ tiền an toàn 100%, thủ tục sang tên đổi chủ tại điểm giao dịch nhà mạng và giải quyết tranh chấp.")]
    public string GetEscrowAndSafetyPolicy()
    {
        return JsonSerializer.Serialize(new
        {
            title = "Quy trình Giao dịch Đảm bảo TrustSim Escrow",
            steps = new[]
            {
                "1. Người mua nạp tiền và đặt cọc/thanh toán giữ tiền trong ví ký quỹ TrustSim.",
                "2. Người bán và người mua cùng ra điểm giao dịch nhà mạng (Viettel/Mobi/Vina) để làm thủ tục sang tên chính chủ.",
                "3. Hệ thống xác thực eKYC/PKI hợp đồng điện tử và Telco API kiểm tra sim đã đổi chủ thành công.",
                "4. Sàn TrustSim tự động mở khóa tiền chuyển cho Người bán. An toàn tuyệt đối 100% không lo lừa đảo."
            },
            disputeProtection = "Nếu bên bán không sang tên hoặc sim có tranh chấp, tiền cọc sẽ được hoàn trả 100% cho người mua."
        });
    }

    [KernelFunction, Description("Tính toán chính xác phí sàn giao dịch và số tiền cọc cần thiết khi mua hoặc đấu giá một số SIM.")]
    public string CalculateTradeFees(
        [Description("Mức giá giao dịch của số SIM (VND)")] decimal price)
    {
        decimal feeRate = price switch
        {
            > 500_000_000 => 0.02m, // 2% cho sim trên 500tr
            > 100_000_000 => 0.03m, // 3%
            _ => 0.05m              // 5% cho phân khúc phổ thông
        };

        decimal feeAmount = price * feeRate;
        decimal depositRequired = price * 0.10m; // Cọc 10%

        return JsonSerializer.Serialize(new
        {
            price = price,
            feeRatePercentage = (feeRate * 100) + "%",
            feeAmount = feeAmount,
            depositRequired = depositRequired,
            netSellerReceives = price - feeAmount
        });
    }
}
