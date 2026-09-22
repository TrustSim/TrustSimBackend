using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu thông tin nhà mạng viễn thông, đầu số cổ / mới, điểm uy tín và chính sách sang tên đổi chủ.
/// </summary>
public class CarrierPolicyPlugin
{
    private readonly ISimCarrierRepository _carrierRepo;
    private readonly IPhonePrefixRepository _prefixRepo;

    public CarrierPolicyPlugin(
        ISimCarrierRepository carrierRepo,
        IPhonePrefixRepository prefixRepo)
    {
        _carrierRepo = carrierRepo;
        _prefixRepo = prefixRepo;
    }

    [KernelFunction, Description("Tra cứu thông tin chi tiết về một nhà mạng viễn thông (Viettel, VinaPhone, MobiFone...), danh sách đầu số và quy trình thủ tục sang tên đổi chủ chính chủ.")]
    public async Task<string> GetCarrierInfoAsync(
        [Description("Tên hoặc mã nhà mạng (ví dụ: Viettel, VinaPhone, MobiFone, Vietnamobile, Wintel, ITelecom)")] string carrierNameOrCode,
        CancellationToken cancellationToken = default)
    {
        var carriers = await _carrierRepo.GetAllAsync(cancellationToken);
        
        var carrier = carriers.FirstOrDefault(c =>
            string.Equals(c.CarrierCode, carrierNameOrCode, StringComparison.OrdinalIgnoreCase) ||
            c.CarrierName.Contains(carrierNameOrCode, StringComparison.OrdinalIgnoreCase));

        if (carrier == null)
        {
            return "{\"error\":\"Không tìm thấy thông tin nhà mạng yêu cầu trong hệ thống.\"}";
        }

        var prefixes = await _prefixRepo.GetAllAsync(carrier.Id, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            carrierId = carrier.Id,
            carrierCode = carrier.CarrierCode,
            carrierName = carrier.CarrierName,
            logoImg = carrier.LogoImg,
            prefixCount = prefixes.Count,
            prefixes = prefixes.Select(p => new
            {
                prefixValue = p.PrefixValue,
                reputationScore = p.PrefixReputationScore
            }),
            transferProcedure = new
            {
                location = $"Tất cả các cửa hàng / điểm giao dịch chính thức của {carrier.CarrierName} trên toàn quốc.",
                requiredDocuments = "Bên bán mang CMND/CCCD gốc + SIM gốc đang sử dụng. Bên mua mang CCCD gắn chip gốc để đăng ký thông tin thuê bao chính chủ.",
                timeRequired = "15 - 30 phút",
                cost = "Phí sang tên nhà mạng thu theo quy định (thường từ 25.000 - 50.000 VND tùy gói cước/trả trước/trả sau).",
                notes = "TrustSim Escrow sẽ hỗ trợ hợp đồng cam kết và giải ngân tiền ngay khi hệ thống xác nhận sang tên thành công."
            }
        });
    }

    [KernelFunction, Description("Tra cứu thông tin, điểm uy tín và ý nghĩa phong thủy của một đầu số điện thoại cụ thể (ví dụ: 098, 090, 091, 088, 086...).")]
    public async Task<string> GetPrefixInfoAsync(
        [Description("Đầu số cần tra cứu (ví dụ: 098, 090, 091, 086, 088, 070...)")] string prefixValue,
        CancellationToken cancellationToken = default)
    {
        string cleanPrefix = (prefixValue ?? string.Empty).Trim();
        if (!cleanPrefix.StartsWith("0") && cleanPrefix.Length == 2)
        {
            cleanPrefix = "0" + cleanPrefix;
        }

        var prefix = await _prefixRepo.GetByValueAsync(cleanPrefix, cancellationToken);
        if (prefix == null)
        {
            return "{\"error\":\"Không tìm thấy thông tin cho đầu số này.\"}";
        }

        bool isVintage = cleanPrefix.StartsWith("098") || cleanPrefix.StartsWith("090") || cleanPrefix.StartsWith("091") || cleanPrefix.StartsWith("097");
        string vintageCategory = isVintage ? "Đầu số cổ danh giá (Thập niên 90 - 2000)" : "Đầu số chuyển đổi / Đầu số mới thế hệ mới";

        return JsonSerializer.Serialize(new
        {
            prefixValue = prefix.PrefixValue,
            carrierName = prefix.SimCarrier?.CarrierName ?? "Viễn thông",
            reputationScore = prefix.PrefixReputationScore,
            category = vintageCategory,
            fengShuiMeaning = GetPrefixFengShuiMeaning(cleanPrefix, prefix.SimCarrier?.CarrierName)
        });
    }


    private static string GetPrefixFengShuiMeaning(string prefix, string? carrierName)
    {
        return prefix switch
        {
            "098" => "Đầu số cổ VIP nhất của Tập đoàn Công nghiệp Viễn thông Quân đội Viettel. Biểu trưng cho quyền lực, trường cửu phát đạt (9: Trường thọ, 8: Phát tài).",
            "090" => "Đầu số cổ đầu tiên của mạng MobiFone và của cả ngành viễn thông di động Việt Nam (từ 1993). Đẳng cấp doanh nhân kỳ cựu.",
            "091" => "Đầu số cổ danh giá của Tập đoàn VNPT VinaPhone (từ 1996). Biểu trưng cho sự tiên phong, dẫn đầu và bền vững.",
            "088" => "Đầu số Vượng Phát (Song Phát 88) của VinaPhone. Rất được giới kinh doanh và buôn bán săn đón.",
            "086" => "Đầu số Phát Lộc (8: Phát, 6: Lộc) của Viettel. Mang lại nhiều may mắn tài lộc và hanh thông sự nghiệp.",
            "089" => "Đầu số Phát Cửu của MobiFone. Tượng trưng cho sự thịnh vượng vĩnh cửu và bền chặt.",
            "070" => "Đầu số chuyển đổi tài lộc của MobiFone (chuyển đổi từ 0120 cũ), thanh lịch và dễ nhớ.",
            _ => $"Đầu số {prefix} thuộc mạng {carrierName ?? "Viễn thông"}, định dạng chuẩn 10 số quốc gia."
        };
    }
}
