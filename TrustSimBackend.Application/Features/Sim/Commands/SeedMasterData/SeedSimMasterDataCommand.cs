using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands.SeedMasterData;

/// <summary>
/// Command khởi tạo dữ liệu nền mẫu (Nhà mạng, Đầu số, Danh mục, Nhịp định dạng) cho hệ thống TrustSim.
/// </summary>
public record SeedSimMasterDataCommand : IRequest<ApiResponse<string>>;

public class SeedSimMasterDataCommandHandler : IRequestHandler<SeedSimMasterDataCommand, ApiResponse<string>>
{
    private readonly ISimMasterDataRepository _masterDataRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SeedSimMasterDataCommandHandler(
        ISimMasterDataRepository masterDataRepository,
        IUnitOfWork unitOfWork)
    {
        _masterDataRepository = masterDataRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<string>> Handle(SeedSimMasterDataCommand request, CancellationToken cancellationToken)
    {
        if (await _masterDataRepository.HasAnyCarrierAsync(cancellationToken))
        {
            return ApiResponse<string>.SuccessResult("Dữ liệu Master Data đã tồn tại trong hệ thống, không cần nạp lại.");
        }

        // ─── 1. Khởi tạo Nhà mạng & Đầu số ───────────────────────────────────
        var viettel = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "Viettel Telecom",
            CarrierCode = "VTT",
            LogoImg = "https://cdn.trustsim.vn/carriers/viettel.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "096", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "097", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "098", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "086", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "032", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "033", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "034", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "035", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "036", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "037", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "038", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "039", IsActive = true }
            }
        };

        var vinaphone = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "VinaPhone",
            CarrierCode = "VNP",
            LogoImg = "https://cdn.trustsim.vn/carriers/vinaphone.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "091", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "094", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "088", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "081", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "082", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "083", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "084", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "085", IsActive = true }
            }
        };

        var mobifone = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "MobiFone",
            CarrierCode = "MBF",
            LogoImg = "https://cdn.trustsim.vn/carriers/mobifone.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "090", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "093", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "089", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "070", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "079", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "077", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "076", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "078", IsActive = true }
            }
        };

        var vietnamobile = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "Vietnamobile",
            CarrierCode = "VNM",
            LogoImg = "https://cdn.trustsim.vn/carriers/vietnamobile.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "092", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "056", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "058", IsActive = true },
                new() { Id = Guid.NewGuid(), PrefixValue = "052", IsActive = true }
            }
        };

        var wintel = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "Wintel (Mobicast)",
            CarrierCode = "WTL",
            LogoImg = "https://cdn.trustsim.vn/carriers/wintel.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "055", IsActive = true }
            }
        };

        var itel = new SimCarrier
        {
            Id = Guid.NewGuid(),
            CarrierName = "iTel (Indochina Telecom)",
            CarrierCode = "ITL",
            LogoImg = "https://cdn.trustsim.vn/carriers/itel.png",
            IsActive = true,
            PhonePrefixes = new List<PhonePrefix>
            {
                new() { Id = Guid.NewGuid(), PrefixValue = "087", IsActive = true }
            }
        };

        await _masterDataRepository.AddCarrierAsync(viettel, cancellationToken);
        await _masterDataRepository.AddCarrierAsync(vinaphone, cancellationToken);
        await _masterDataRepository.AddCarrierAsync(mobifone, cancellationToken);
        await _masterDataRepository.AddCarrierAsync(vietnamobile, cancellationToken);
        await _masterDataRepository.AddCarrierAsync(wintel, cancellationToken);
        await _masterDataRepository.AddCarrierAsync(itel, cancellationToken);

        // ─── 2. Khởi tạo Danh mục phân loại SIM (Categories) ─────────────────
        var categories = new List<SimCategory>
        {
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "LUC_QUY",
                CategoryName = "SIM Lục Quý",
                Description = "SIM có 6 chữ số cuối giống nhau hoàn toàn (VD: 09x.888888, 09x.999999). Dòng SIM siêu VIP và quý hiếm bậc nhất.",
                ValuationMultiplier = 5.0f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "NGU_QUY",
                CategoryName = "SIM Ngũ Quý",
                Description = "SIM có 5 chữ số cuối giống nhau hoàn toàn (VD: 09x.x88888, 09x.x99999). Mang ý nghĩa đẳng cấp và quyền lực tuyệt đối.",
                ValuationMultiplier = 3.5f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "TU_QUY",
                CategoryName = "SIM Tứ Quý",
                Description = "SIM có 4 chữ số cuối giống nhau (VD: 09x.xx8888, 09x.xx9999, 09x.xx6666). Biểu trưng cho may mắn, vững chãi bốn mùa.",
                ValuationMultiplier = 2.5f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "SANH_TIEN",
                CategoryName = "SIM Sảnh Tiến",
                Description = "SIM có dãy số tăng liên tiếp ở đuôi (VD: 09x.xxx.56789, 09x.xxx.6789, 09x.xxx.1234). Tượng trưng cho sự thăng tiến không ngừng trong sự nghiệp.",
                ValuationMultiplier = 2.2f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "THAN_TAI",
                CategoryName = "SIM Thần Tài",
                Description = "SIM có đuôi chứa cặp số Thần Tài nhỏ (39), Thần Tài lớn (79) hoặc Thần Tài kép (3979, 7979). Đem lại tài lộc và vận may kinh doanh.",
                ValuationMultiplier = 1.8f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "LOC_PHAT",
                CategoryName = "SIM Lộc Phát",
                Description = "SIM có đuôi chứa cặp số 68 (Lộc Phát), 86 (Phát Lộc), 6868, 8686. Rất được giới doanh nhân và kinh doanh ưa chuộng.",
                ValuationMultiplier = 1.8f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "ONG_DIA",
                CategoryName = "SIM Ông Địa",
                Description = "SIM chứa cặp số 38 (Ông Địa nhỏ) hoặc 78 (Ông Địa lớn). Tượng trưng cho sự bảo hộ về đất đai và kinh doanh thuận lợi.",
                ValuationMultiplier = 1.5f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "TAM_HOA",
                CategoryName = "SIM Tam Hoa",
                Description = "SIM có 3 chữ số cuối giống nhau (VD: 09x.xxx.666, 09x.xxx.888, 09x.xxx.999) hoặc Tam Hoa kép.",
                ValuationMultiplier = 1.4f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "TAXI",
                CategoryName = "SIM Taxi",
                Description = "SIM dạng lặp 2 hoặc 3 cụm số (VD: 09x.789.789, 09x.86.86.86). Dễ nhớ tuyệt đối, thích hợp cho doanh nghiệp làm Hotline.",
                ValuationMultiplier = 1.6f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "GANH_DAO",
                CategoryName = "SIM Gánh Đảo",
                Description = "SIM có các cặp số đối xứng gánh nhau (VD: 09x.xxx.8998, 09x.xxx.6886). Mang ý nghĩa cân bằng và hóa giải điềm xấu.",
                ValuationMultiplier = 1.3f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "NAM_SINH",
                CategoryName = "SIM Năm Sinh",
                Description = "SIM có 4 số đuôi trùng với năm sinh (VD: 09x.xxx.1998, 09x.xxx.2003). Gắn liền với dấu ấn cá nhân.",
                ValuationMultiplier = 1.2f,
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                CategoryCode = "PHONG_THUY",
                CategoryName = "SIM Phong Thủy",
                Description = "SIM được tính toán theo Ngũ Hành, Bát Tự và Quẻ Dịch, phù hợp với mệnh của từng gia chủ.",
                ValuationMultiplier = 1.3f,
                IsActive = true
            }
        };

        foreach (var category in categories)
        {
            await _masterDataRepository.AddCategoryAsync(category, cancellationToken);
        }

        // ─── 3. Khởi tạo Kiểu nhịp định dạng (Format Rhythms) ────────────────
        var rhythms = new List<FormatRhythm>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Value = "ABCDE",
                FormatRhythmName = "Sảnh tiến liên tục",
                Description = "Chuỗi số tăng liên tục (VD: 090.123.4567, 098.567.8999)",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Value = "AAAA",
                FormatRhythmName = "Đồng nhất (Tứ/Ngũ quý)",
                Description = "Các số lặp lại giống nhau ở đuôi (VD: 090.888.8888, 091.999.9999)",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Value = "ABABAB",
                FormatRhythmName = "Lặp đôi liên tiếp (Taxi)",
                Description = "Các cặp số lặp luân phiên (VD: 090.88.99.88, 093.79.79.79)",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Value = "AAABBB",
                FormatRhythmName = "Tam hoa kép",
                Description = "Hai cụm tam hoa đứng cạnh nhau (VD: 090.666.888, 098.333.999)",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Value = "ABBA",
                FormatRhythmName = "Gánh đảo đối xứng",
                Description = "Cấu trúc gánh đối xứng qua tâm (VD: 090.8998, 091.6886)",
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Value = "STANDARD",
                FormatRhythmName = "Tiêu chuẩn tự do",
                Description = "Cấu trúc số thông thường, chia nhịp 3-3-4 hoặc 4-3-3",
                IsActive = true
            }
        };

        foreach (var rhythm in rhythms)
        {
            await _masterDataRepository.AddFormatRhythmAsync(rhythm, cancellationToken);
        }

        // ─── 4. Lưu toàn bộ xuống DB ─────────────────────────────────────────
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<string>.SuccessResult("Khởi tạo dữ liệu Master Data thành công.");
    }
}
