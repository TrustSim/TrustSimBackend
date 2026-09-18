using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Sim.Commands;

public class SeedSimMasterDataCommandHandler : IRequestHandler<SeedSimMasterDataCommand, string>
{
    private readonly IUnitOfWork _unitOfWork;

    public SeedSimMasterDataCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<string> Handle(SeedSimMasterDataCommand request, CancellationToken cancellationToken)
    {
        var hasCarriers = await _unitOfWork.SimMasterData.HasAnyCarrierAsync(cancellationToken);
        if (hasCarriers)
        {
            return "Dữ liệu Master Data đã tồn tại trên hệ thống, không cần nạp lại.";
        }

        // 1. FormatRhythms
        var rhythms = new List<FormatRhythm>
        {
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000001"), FormatRhythmName = "Nhịp đều 5 cặp (Đại Taxi / Lặp 5)", Value = "2.2.2.2.2", BasePriceFactor = 1.30f, Description = "Ngắt 5 block 2 số làm nổi bật 4 cặp thế số hoặc lặp đôi liên tiếp." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000002"), FormatRhythmName = "Nhịp cụm đôi 4-4 (Đôi Bát Tứ)",   Value = "2.4.4",     BasePriceFactor = 1.25f, Description = "Ngắt 09.xxxx.xxxx làm nổi bật 2 cụm tứ quý hoặc 2 cụm sảnh 4." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000003"), FormatRhythmName = "Nhịp Bát quý / Sảnh dài",         Value = "2.8",       BasePriceFactor = 1.35f, Description = "Ngắt 09.xxxxxxxx phô diễn toàn bộ 8 số VIP liên tiếp phía sau." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000004"), FormatRhythmName = "Nhịp Lục quý đuôi",             Value = "4.6",       BasePriceFactor = 1.25f, Description = "Ngắt 09xx.xxxxxx làm nổi bật trọn vẹn cụm lục quý 6 số đuôi." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000005"), FormatRhythmName = "Nhịp Tam hoa kép",                 Value = "4.3.3",     BasePriceFactor = 1.20f, Description = "Ngắt 09xx.xxx.xxx làm nổi bật 2 bộ tam hoa liên tiếp." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000006"), FormatRhythmName = "Nhịp 3 cặp + Tứ quý đuôi",        Value = "2.2.2.4",   BasePriceFactor = 1.20f, Description = "Ngắt 09.xx.xx.xxxx làm nổi bật tứ quý đuôi." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000007"), FormatRhythmName = "Nhịp kẹp Tứ quý giữa",          Value = "3.4.3",     BasePriceFactor = 1.15f, Description = "Ngắt 09x.xxxx.xxx làm nổi bật cụm 4 số ở giữa." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000008"), FormatRhythmName = "Nhịp Gánh đôi cân xứng",         Value = "3.2.2.3",   BasePriceFactor = 1.10f, Description = "Ngắt 09x.xx.xx.xxx cấu trúc gánh đối xứng." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000009"), FormatRhythmName = "Nhịp Song long / Cân bằng",       Value = "5.5",       BasePriceFactor = 1.15f, Description = "Ngắt 09xxx.xxxxx chia đều 2 nửa cân bằng." },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000010"), FormatRhythmName = "Nhịp chuẩn viễn thông",          Value = "3.3.4",     BasePriceFactor = 1.00f, Description = "Định dạng ngắt mặc định tiêu chuẩn 09x.xxx.xxxx" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000011"), FormatRhythmName = "Nhịp Phổ thông",                 Value = "NORMAL",    BasePriceFactor = 1.00f, Description = "Số thông thường không thuộc các thế nhịp đặc thù" }
        };
        foreach (var r in rhythms)
        {
            await _unitOfWork.SimMasterData.AddFormatRhythmAsync(r, cancellationToken);
        }

        // 2. SimCarriers
        var carriers = new List<SimCarrier>
        {
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"), CarrierCode = "VTT", CarrierName = "Viettel", LogoImg = "https://cdn.trustsim.vn/carriers/viettel.png" },
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000002"), CarrierCode = "VNP", CarrierName = "Vinaphone", LogoImg = "https://cdn.trustsim.vn/carriers/vinaphone.png" },
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000003"), CarrierCode = "VMS", CarrierName = "Mobifone", LogoImg = "https://cdn.trustsim.vn/carriers/mobifone.png" },
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000004"), CarrierCode = "VNM", CarrierName = "Vietnamobile", LogoImg = "https://cdn.trustsim.vn/carriers/vietnamobile.png" },
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000005"), CarrierCode = "GML", CarrierName = "Gmobile", LogoImg = "https://cdn.trustsim.vn/carriers/gmobile.png" },
            new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000006"), CarrierCode = "WNT", CarrierName = "Wintel", LogoImg = "https://cdn.trustsim.vn/carriers/wintel.png" },
        };
        foreach (var c in carriers)
        {
            await _unitOfWork.SimMasterData.AddCarrierAsync(c, cancellationToken);
        }

        // 3. SimCategories
        var categories = new List<SimCategory>
        {
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000001"), CategoryCode = "BAT_QUY",        CategoryName = "SIM Bát quý",                   Description = "Đẳng cấp siêu VIP 8 số trùng lặp", ValuationMultiplier = 20.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000002"), CategoryCode = "LUC_QUY",        CategoryName = "SIM Lục quý",                   Description = "6 chữ số giống nhau liên tiếp", ValuationMultiplier = 12.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000003"), CategoryCode = "NGU_QUY",        CategoryName = "SIM Ngũ quý",                   Description = "5 chữ số giống nhau liên tiếp", ValuationMultiplier = 7.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000004"), CategoryCode = "TU_QUY",         CategoryName = "SIM Tứ quý",                    Description = "4 chữ số giống nhau đuôi số", ValuationMultiplier = 4.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000005"), CategoryCode = "SANH_TIEN",      CategoryName = "SIM Sảnh tiến",                 Description = "Chuỗi số tiến đều mang ý nghĩa thăng tiến", ValuationMultiplier = 3.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000006"), CategoryCode = "THAN_TAI",       CategoryName = "SIM Thần tài",                  Description = "Số đuôi 79, 39 mang lại may mắn", ValuationMultiplier = 2.2f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000007"), CategoryCode = "LOC_PHAT",       CategoryName = "SIM Lộc phát",                  Description = "Chứa số 68, 86 phát tài phát lộc", ValuationMultiplier = 2.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000008"), CategoryCode = "LAP_DOI",        CategoryName = "SIM Lặp đôi",                   Description = "Hai cặp số lặp lại nhịp nhàng", ValuationMultiplier = 2.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000009"), CategoryCode = "PHONG_THUY",     CategoryName = "SIM Phong thủy",                Description = "Chứa nhiều chữ số hợp mệnh ngũ hành", ValuationMultiplier = 1.8f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000010"), CategoryCode = "SIM_THUONG",     CategoryName = "SIM Phổ thông",                 Description = "Số điện thoại thông thường", ValuationMultiplier = 1.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000011"), CategoryCode = "LOC_PHAT_KEP",   CategoryName = "SIM Lộc phát kép (6868)",       Description = "Đại lộc phát tài, nhân đôi vượng khí thương trường", ValuationMultiplier = 3.5f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000012"), CategoryCode = "CAT_TUONG_1368", CategoryName = "SIM Sinh tài lộc phát (1368)", Description = "Cát tường đỉnh cao: Sinh tài lộc phát trọn vẹn bốn mùa", ValuationMultiplier = 3.5f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000013"), CategoryCode = "THAN_TAI_KEP",   CategoryName = "SIM Thần tài kép (3979)",       Description = "Thần tài lớn nhỏ tương hỗ, công danh rực rỡ", ValuationMultiplier = 3.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000014"), CategoryCode = "THAN_TAI_LON",   CategoryName = "SIM Thần tài lớn (79)",         Description = "Đuôi 79 phù trợ kinh doanh buôn bán đại thắng", ValuationMultiplier = 2.2f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000015"), CategoryCode = "THAN_TAI_NHO",   CategoryName = "SIM Thần tài nhỏ (39)",         Description = "Đuôi 39 hanh thông may mắn tài lộc", ValuationMultiplier = 1.8f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000016"), CategoryCode = "ONG_DIA_KEP",   CategoryName = "SIM Ông Địa kép (3878)",        Description = "Vượng khí đất đai, đắc lợi cho giới bất động sản", ValuationMultiplier = 2.2f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000017"), CategoryCode = "ONG_DIA",       CategoryName = "SIM Ông Địa (38/78)",           Description = "Đuôi 38 hoặc 78 bảo hộ gia đạo và điền trạch", ValuationMultiplier = 1.6f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000018"), CategoryCode = "SIM_NAM_SINH",   CategoryName = "SIM Năm sinh",                  Description = "Dãy số mang mốc năm sinh thể hiện bản sắc cá nhân", ValuationMultiplier = 1.7f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000019"), CategoryCode = "KHONG_THAT_BAT", CategoryName = "SIM Không 4-7 (Sạch số)",      Description = "Số sạch hoàn toàn không dính 4 và 7, vượng khí thuần khiết", ValuationMultiplier = 1.35f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000020"), CategoryCode = "TAM_HOA",        CategoryName = "SIM Tam hoa",                   Description = "Bộ ba số giống nhau vững chãi như kiềng ba chân", ValuationMultiplier = 1.8f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000021"), CategoryCode = "GANH_DAO",       CategoryName = "SIM Gánh đảo",                  Description = "Cấu trúc gánh đối xứng cân bằng âm dương", ValuationMultiplier = 1.4f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000022"), CategoryCode = "DAI_TAXI_86",     CategoryName = "SIM Đại Taxi Lộc Phát (86)",    Description = "Cực phẩm 4 cặp 86 liên tiếp, đại phú đại quý tột đỉnh", ValuationMultiplier = 10.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000023"), CategoryCode = "DAI_TAXI_68",     CategoryName = "SIM Đại Taxi Phát Lộc (68)",    Description = "Cực phẩm 4 cặp 68 liên tiếp, vượng khí kinh doanh ngút trời", ValuationMultiplier = 9.0f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000024"), CategoryCode = "LOC_PHAT_86",     CategoryName = "SIM Phát Lộc (86)",             Description = "Cặp số 86 tượng trưng cho sự phát triển tài lộc", ValuationMultiplier = 2.2f },
            new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000025"), CategoryCode = "LOC_PHAT_68",     CategoryName = "SIM Lộc Phát (68)",             Description = "Cặp số 68 tượng trưng cho lộc tài hưng vượng", ValuationMultiplier = 2.0f }
        };
        foreach (var cat in categories)
        {
            await _unitOfWork.SimMasterData.AddCategoryAsync(cat, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return "Nạp Master Data chuẩn viễn thông Việt Nam thành công.";
    }
}
