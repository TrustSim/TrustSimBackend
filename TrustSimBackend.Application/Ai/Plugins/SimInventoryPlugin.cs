using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Ai.Plugins;

/// <summary>
/// Plugin tra cứu kho SIM thực tế đang mở bán trên sàn TrustSim.
/// </summary>
public class SimInventoryPlugin
{
    private readonly ISimNumberRepository _simRepo;

    public SimInventoryPlugin(ISimNumberRepository simRepo)
    {
        _simRepo = simRepo;
    }

    [KernelFunction, Description("Tìm kiếm danh sách số SIM thực tế đang mở bán trên sàn TrustSim theo ngân sách, nhà mạng, đầu số hoặc mẫu số. CHỈ gọi khi người dùng muốn tìm SIM mua hoặc cần gợi ý danh sách số có thể mua.")]
    public async Task<string> SearchAvailableSimsAsync(
        [Description("Mức giá tối thiểu bằng VND (ví dụ: 10000000)")] decimal? minPrice = null,
        [Description("Mức giá tối đa bằng VND (ví dụ: 50000000)")] decimal? maxPrice = null,
        [Description("Tên nhà mạng (Viettel, VinaPhone, MobiFone...)")] string? carrier = null,
        [Description("Đầu số (098, 090, 091, 088...)")] string? prefix = null,
        [Description("Mẫu số hoặc đuôi số (ví dụ: 68, 79, 8686...)")] string? keyword = null,
        CancellationToken cancellationToken = default)
    {
        var sims = await _simRepo.GetAvailableSimsForConsultationAsync(minPrice, maxPrice, carrier, prefix, keyword, limit: 6, cancellationToken);
        if (sims.Count == 0)
        {
            return "{\"total\":0,\"items\":[],\"message\":\"Không tìm thấy số SIM phù hợp với tiêu chí trong kho hiện tại.\"}";
        }

        return JsonSerializer.Serialize(new
        {
            total = sims.Count,
            items = sims.Select(s => new
            {
                simNumberId = s.Id,
                phoneNumber = s.PhoneNumber,
                price = s.Price,
                carrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
                highlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp phong thủy"} - Đầu {s.PhonePrefix?.PrefixValue}"
            })
        });
    }

    [KernelFunction, Description("Lấy thông tin chi tiết một số SIM cụ thể trên sàn (giá bán, trạng thái xác minh, nhà mạng, thế số).")]
    public async Task<string> GetSimDetailsAsync(
        [Description("Số điện thoại cần xem chi tiết (ví dụ: 0986868686)")] string phoneNumber,
        CancellationToken cancellationToken = default)
    {
        var sim = await _simRepo.GetByPhoneNumberAsync(phoneNumber, cancellationToken);
        if (sim == null) return "{\"error\":\"Không tìm thấy số SIM này trong hệ thống.\"}";

        return JsonSerializer.Serialize(new
        {
            simNumberId = sim.Id,
            phoneNumber = sim.PhoneNumber,
            price = sim.Price,
            status = sim.SimStatus.ToString(),
            carrierName = sim.PhonePrefix?.SimCarrier?.CarrierName,
            rhythmName = sim.FormatRhythm?.FormatRhythmName,
            isVerified = sim.SimStatus != TrustSimBackend.Domain.Enums.SimStatus.UNVERIFIED
        });
    }
}
