using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Infrastructure.Services.Financial;

namespace TrustSimBackend.Infrastructure.Persistence;

/// <summary>
/// Nạp dữ liệu ban đầu (Master Data, AI SIM Valuation RAG Knowledge & Test Scenarios) vào Database khi ứng dụng khởi động.
/// Tuân thủ quy chuẩn cây phụ thuộc (Level 0 -> Level 6) và đảm bảo Idempotent (chạy lại không lỗi).
/// Tất cả GUID sử dụng chuỗi Hexadecimal chuẩn [0-9a-f].
/// </summary>
public static class DbSeeder
{
    // Hash mật khẩu "Password123@" theo chuẩn ASP.NET Core Identity PBKDF2
    private static readonly string DefaultPasswordHash = new PasswordHasher<UserAccount>().HashPassword(null!, "Password123@");

    public static async Task SeedAsync(TrustSimDbContext context, IServiceProvider? serviceProvider = null)
    {
        // 0. Seed ASP.NET Identity Roles
        await SeedRolesAsync(context);

        // 1. Level 0: Bảng danh mục cơ bản & Documents
        await SeedMasterCatalogAsync(context);

        // 2. Level 1: Đầu số (PhonePrefixes)
        await SeedPhonePrefixesAsync(context);

        // 3. Level 0 & 1: Danh sách 10 Tài khoản, Hồ sơ, Cặp khóa PKI & Ví điện tử
        await SeedAccountsProfilesAndWalletsAsync(context);

        // 4. Level 2 & 3: Kho SIM số đẹp, Phân loại danh mục & Quyền sở hữu ban đầu
        await SeedSimsAndOwnershipsAsync(context);

        // 5. Level 3 & 4: Tri thức RAG nghiên cứu thị trường & Lịch sử Định giá AI
        await SeedAiValuationAndRagDocumentsAsync(context, serviceProvider);

        // 6. Level 4 & 5: Các phiên đấu giá (English, Dutch, End), Quy tắc, Người tham gia, Bids & Gian lận
        await SeedAuctionsAndBidsAsync(context);

        // 7. Level 4 -> 6: Giao dịch chuyển nhượng, Hợp đồng số, Ký quỹ Escrow, Chữ ký số & Bằng chứng
        await SeedTradeAndContractsAsync(context);

        // 8. Level 6: Thông báo người dùng & Nhật ký kiểm toán hệ thống
        await SeedNotificationsAndAuditLogsAsync(context);

        Console.WriteLine("[DbSeeder] ✅ Toàn bộ Seed Data đa kịch bản và AI SIM Valuation đã được nạp thành công.");
    }

    // ─────────────────────────────────────────────────────────────
    //  1. Level 0: Master Catalog (Nhà mạng, Nhịp số, Danh mục, Docs)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedMasterCatalogAsync(TrustSimDbContext context)
    {
        // 1. FormatRhythms (f1000000-...) - Danh mục các mẫu ngắt nhịp số (Cadence Partition Templates)
        var rhythms = new List<FormatRhythm>
        {
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000001"), FormatRhythmName = "Nhịp đều 5 cặp (Đại Taxi / Lặp 5)", Value = "2.2.2.2.2", BasePriceFactor = 1.30f, Description = "Ngắt 5 block 2 số (09.xx.xx.xx.xx) làm nổi bật 4 cặp thế số hoặc lặp đôi liên tiếp. VD: 09.86.86.86.86" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000002"), FormatRhythmName = "Nhịp cụm đôi 4-4 (Đôi Bát Tứ)",   Value = "2.4.4",     BasePriceFactor = 1.25f, Description = "Ngắt 09.xxxx.xxxx làm nổi bật 2 cụm tứ quý hoặc 2 cụm sảnh 4. VD: 09.6666.8888" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000003"), FormatRhythmName = "Nhịp Bát quý / Sảnh dài",         Value = "2.8",       BasePriceFactor = 1.35f, Description = "Ngắt 09.xxxxxxxx phô diễn toàn bộ 8 số VIP liên tiếp phía sau. VD: 09.88888888, 09.12345678" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000004"), FormatRhythmName = "Nhịp Lục quý đuôi",             Value = "4.6",       BasePriceFactor = 1.25f, Description = "Ngắt 09xx.xxxxxx làm nổi bật trọn vẹn cụm lục quý 6 số đuôi. VD: 0909.999999" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000005"), FormatRhythmName = "Nhịp Tam hoa kép",                 Value = "4.3.3",     BasePriceFactor = 1.20f, Description = "Ngắt 09xx.xxx.xxx làm nổi bật 2 bộ tam hoa liên tiếp. VD: 0988.666.888" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000006"), FormatRhythmName = "Nhịp 3 cặp + Tứ quý đuôi",        Value = "2.2.2.4",   BasePriceFactor = 1.20f, Description = "Ngắt 09.xx.xx.xxxx làm nổi bật tứ quý đuôi. VD: 09.12.34.8888" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000007"), FormatRhythmName = "Nhịp kẹp Tứ quý giữa",          Value = "3.4.3",     BasePriceFactor = 1.15f, Description = "Ngắt 09x.xxxx.xxx làm nổi bật cụm 4 số ở giữa. VD: 098.8888.686" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000008"), FormatRhythmName = "Nhịp Gánh đôi cân xứng",         Value = "3.2.2.3",   BasePriceFactor = 1.10f, Description = "Ngắt 09x.xx.xx.xxx cấu trúc gánh đối xứng. VD: 098.68.68.098" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000009"), FormatRhythmName = "Nhịp Song long / Cân bằng",       Value = "5.5",       BasePriceFactor = 1.15f, Description = "Ngắt 09xxx.xxxxx chia đều 2 nửa cân bằng. VD: 09888.88888" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000010"), FormatRhythmName = "Nhịp chuẩn viễn thông",          Value = "3.3.4",     BasePriceFactor = 1.00f, Description = "Định dạng ngắt mặc định tiêu chuẩn 09x.xxx.xxxx" },
            new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000011"), FormatRhythmName = "Nhịp Phổ thông",                 Value = "NORMAL",    BasePriceFactor = 1.00f, Description = "Số thông thường không thuộc các thế nhịp đặc thù" }
        };

        foreach (var r in rhythms)
        {
            var existing = await context.FormatRhythms.FirstOrDefaultAsync(x => x.Id == r.Id || x.Value == r.Value);
            if (existing == null)
            {
                await context.FormatRhythms.AddAsync(r);
            }
            else
            {
                existing.FormatRhythmName = r.FormatRhythmName;
                existing.BasePriceFactor = r.BasePriceFactor;
                existing.Description = r.Description;
            }
        }
        await context.SaveChangesAsync();

        // 2. SimCategories (ca000000-...) - Danh mục tổ hợp số & ý nghĩa phong thủy
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

        foreach (var c in categories)
        {
            var existing = await context.SimCategories.FirstOrDefaultAsync(x => x.Id == c.Id || x.CategoryCode == c.CategoryCode);
            if (existing == null)
            {
                await context.SimCategories.AddAsync(c);
            }
            else
            {
                existing.CategoryName = c.CategoryName;
                existing.ValuationMultiplier = c.ValuationMultiplier;
                existing.Description = c.Description;
            }
        }
        await context.SaveChangesAsync();

        // 3. SimCarriers (c1000000-...)
        if (!await context.SimCarriers.AnyAsync())
        {
            var carriers = new List<SimCarrier>
            {
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"), CarrierName = "Viettel Telecom",          CarrierCode = "VTT", LogoImg = "https://assets.trustsim.vn/carriers/viettel.png" },
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000002"), CarrierName = "VinaPhone",                CarrierCode = "VNP", LogoImg = "https://assets.trustsim.vn/carriers/vinaphone.png" },
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000003"), CarrierName = "MobiFone",                 CarrierCode = "MBF", LogoImg = "https://assets.trustsim.vn/carriers/mobifone.png" },
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000004"), CarrierName = "Vietnamobile",             CarrierCode = "VNM", LogoImg = "https://assets.trustsim.vn/carriers/vietnamobile.png" },
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000005"), CarrierName = "Wintel (Reddi)",           CarrierCode = "RDD", LogoImg = "https://assets.trustsim.vn/carriers/wintel.png" },
                new() { Id = Guid.Parse("c1000000-0000-0000-0000-000000000006"), CarrierName = "Indochina Telecom (ITelecom)", CarrierCode = "ICT", LogoImg = "https://assets.trustsim.vn/carriers/itelecom.png" }
            };
            await context.SimCarriers.AddRangeAsync(carriers);
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  2. Level 1: PhonePrefixes (cb000000-...)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedPhonePrefixesAsync(TrustSimDbContext context)
    {
        var vttId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        var vnpId = Guid.Parse("c1000000-0000-0000-0000-000000000002");
        var mbfId = Guid.Parse("c1000000-0000-0000-0000-000000000003");
        var vnmId = Guid.Parse("c1000000-0000-0000-0000-000000000004");
        var rddId = Guid.Parse("c1000000-0000-0000-0000-000000000005");
        var ictId = Guid.Parse("c1000000-0000-0000-0000-000000000006");

        var prefixes = new List<PhonePrefix>
        {
            // ─── Viettel Telecom (VTT) ──────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000001"), PrefixValue = "098", PrefixReputationScore = 1.50f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000002"), PrefixValue = "097", PrefixReputationScore = 1.45f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000003"), PrefixValue = "086", PrefixReputationScore = 1.25f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000008"), PrefixValue = "096", PrefixReputationScore = 1.40f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000009"), PrefixValue = "032", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000010"), PrefixValue = "033", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000011"), PrefixValue = "034", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000012"), PrefixValue = "035", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000013"), PrefixValue = "036", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000014"), PrefixValue = "037", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000015"), PrefixValue = "038", PrefixReputationScore = 1.10f, SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000016"), PrefixValue = "039", PrefixReputationScore = 1.10f, SimCarrierId = vttId },

            // ─── VinaPhone (VNP) ────────────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000004"), PrefixValue = "091", PrefixReputationScore = 1.45f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000005"), PrefixValue = "088", PrefixReputationScore = 1.30f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000017"), PrefixValue = "094", PrefixReputationScore = 1.40f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000018"), PrefixValue = "081", PrefixReputationScore = 1.10f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000019"), PrefixValue = "082", PrefixReputationScore = 1.10f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000020"), PrefixValue = "083", PrefixReputationScore = 1.10f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000021"), PrefixValue = "084", PrefixReputationScore = 1.10f, SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000022"), PrefixValue = "085", PrefixReputationScore = 1.10f, SimCarrierId = vnpId },

            // ─── MobiFone (MBF) ─────────────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000006"), PrefixValue = "090", PrefixReputationScore = 1.50f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000007"), PrefixValue = "079", PrefixReputationScore = 1.20f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000023"), PrefixValue = "093", PrefixReputationScore = 1.45f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000024"), PrefixValue = "089", PrefixReputationScore = 1.25f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000025"), PrefixValue = "070", PrefixReputationScore = 1.15f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000026"), PrefixValue = "076", PrefixReputationScore = 1.15f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000027"), PrefixValue = "077", PrefixReputationScore = 1.15f, SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000028"), PrefixValue = "078", PrefixReputationScore = 1.15f, SimCarrierId = mbfId },

            // ─── Vietnamobile (VNM) ─────────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000029"), PrefixValue = "092", PrefixReputationScore = 1.00f, SimCarrierId = vnmId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000030"), PrefixValue = "052", PrefixReputationScore = 0.95f, SimCarrierId = vnmId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000031"), PrefixValue = "054", PrefixReputationScore = 0.95f, SimCarrierId = vnmId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000032"), PrefixValue = "056", PrefixReputationScore = 0.95f, SimCarrierId = vnmId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000033"), PrefixValue = "058", PrefixReputationScore = 0.95f, SimCarrierId = vnmId },

            // ─── Wintel - Reddi (RDD) ───────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000034"), PrefixValue = "055", PrefixReputationScore = 0.95f, SimCarrierId = rddId },

            // ─── ITelecom (ICT) ─────────────────────────────────
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000035"), PrefixValue = "087", PrefixReputationScore = 0.95f, SimCarrierId = ictId }
        };

        foreach (var p in prefixes)
        {
            var existing = await context.PhonePrefixes.FirstOrDefaultAsync(x => x.PrefixValue == p.PrefixValue);
            if (existing == null)
            {
                await context.PhonePrefixes.AddAsync(p);
            }
            else
            {
                existing.PrefixReputationScore = p.PrefixReputationScore;
                existing.SimCarrierId = p.SimCarrierId;
            }
        }
        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  0. ASP.NET Core Identity Roles (ADMIN, USER, CONSULTANT)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedRolesAsync(TrustSimDbContext context)
    {
        var roles = new[] { "ADMIN", "USER", "CONSULTANT" };
        foreach (var roleName in roles)
        {
            if (!await context.Roles.AnyAsync(r => r.NormalizedName == roleName))
            {
                await context.Roles.AddAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName,
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                });
            }
        }
        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  3. Level 0 & 1: 10 Tài khoản, Hồ sơ, Cặp khóa PKI & Ví tiền
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAccountsProfilesAndWalletsAsync(TrustSimDbContext context)
    {
        // Tự động nâng cấp các tài khoản đang lưu mã băm BCrypt cũ sang chuẩn ASP.NET Core Identity
        var legacyUsers = await context.UserAccounts
            .Where(u => u.PasswordHash != null && u.PasswordHash.StartsWith("$2"))
            .ToListAsync();
        if (legacyUsers.Count > 0)
        {
            foreach (var lu in legacyUsers)
            {
                lu.PasswordHash = DefaultPasswordHash;
            }
            await context.SaveChangesAsync();
        }

        // 2. Lấy danh mục RoleId chuẩn từ bảng Roles
        var roles = await context.Roles.ToListAsync();
        var roleDict = roles.ToDictionary(r => (r.NormalizedName ?? r.Name!).ToUpperInvariant(), r => r.Id);

        var usersData = new[]
        {
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000001"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000001"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000001"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000001"), UserName = "admin",               Email = "admin@trustsim.vn",          Role = UserRole.ADMIN,      FullName = "Tổng Quản Trị Hệ Thống",          Phone = "0900000001", Identity = "001088000001", Dob = new DateTime(1988, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 1000000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000002"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000002"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000002"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000002"), UserName = "consultant_01",       Email = "consultant01@trustsim.vn",  Role = UserRole.CONSULTANT, FullName = "Lê Hoàng Chuyên Viên",              Phone = "0900000002", Identity = "079092000002", Dob = new DateTime(1992, 5, 15, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000003"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000003"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000003"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000003"), UserName = "seller_vip_phuquy",   Email = "supperidol6224@gmail.com",   Role = UserRole.USER,       FullName = "Trần Phú Quý (SIM Phú Quý)",       Phone = "0909999001", Identity = "001085009999", Dob = new DateTime(1985, 10, 20, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 1000000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000004"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000004"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000004"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000004"), UserName = "seller_daiphat",      Email = "daiphat.telecom@gmail.com",  Role = UserRole.USER,       FullName = "Nguyễn Đại Phát (Đại Phát Telecom)",Phone = "0912333002", Identity = "079087008888", Dob = new DateTime(1987, 3, 12, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 1000000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000005"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000005"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000005"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000005"), UserName = "bidder_dai_gia",      Email = "quanidol6224@gmail.com",     Role = UserRole.USER,       FullName = "Phạm Hoàng Nam (Đầu Tư BĐS)",      Phone = "0988777003", Identity = "001080007777", Dob = new DateTime(1980, 7, 8, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 5000000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000006"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000006"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000006"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000006"), UserName = "bidder_kinh_doanh",  Email = "minhtri.bds@gmail.com",      Role = UserRole.USER,       FullName = "Vũ Minh Trí (Kinh Doanh)",         Phone = "0903444004", Identity = "079090006666", Dob = new DateTime(1990, 11, 25, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 2000000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000007"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000007"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000007"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000007"), UserName = "bidder_phong_thuy",  Email = "quangduc.fengshui@gmail.com",Role = UserRole.USER,       FullName = "Đặng Quang Đức (Phong Thủy Gia)",  Phone = "0935555005", Identity = "048083005555", Dob = new DateTime(1983, 9, 9, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 1000000000m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000011"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000011"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000011"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000011"), UserName = "bidder_alpha",       Email = "alpha.bidder@gmail.com",     Role = UserRole.USER,       FullName = "Hoàng Gia Bảo (Đấu Giá Viên Alpha)",Phone = "0981111222", Identity = "001095001111", Dob = new DateTime(1995, 2, 2, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 500000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000012"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000012"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000012"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000012"), UserName = "bidder_beta",        Email = "beta.bidder@gmail.com",      Role = UserRole.USER,       FullName = "Lê Anh Tuấn (Á Quân Thiếu Tiền)",  Phone = "0983333444", Identity = "001096002222", Dob = new DateTime(1996, 6, 6, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 12000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000013"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000013"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000013"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000013"), UserName = "bidder_moderate_funds", Email = "moderate@gmail.com",         Role = UserRole.USER,       FullName = "Trần Văn Cọc (Đấu Giá Viên Vừa Đủ Cọc)", Phone = "0984444555", Identity = "001097003333", Dob = new DateTime(1997, 7, 7, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 15000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000008"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000008"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000008"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000008"), UserName = "user_ekyc_pending",  Email = "thanhha.new@gmail.com",      Role = UserRole.USER,       FullName = "Bùi Thanh Hà",                     Phone = "0977111006", Identity = "001098004444", Dob = new DateTime(1998, 12, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.PENDING_VERIFICATION, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000009"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000009"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000009"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000009"), UserName = "user_ekyc_rejected", Email = "vietcuong.fake@gmail.com",     Role = UserRole.USER,       FullName = "Ngô Việt Cường",                    Phone = "0944222007", Identity = "040095003333", Dob = new DateTime(1995, 4, 4, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.REJECTED, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000010"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000010"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000010"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000010"), UserName = "user_suspicious_bot", Email = "spambidder99@botmail.com",   Role = UserRole.USER,       FullName = "Bot Nghi Vấn Spam",                Phone = "0966000008", Identity = "001200002222", Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = false }
        };

        foreach (var u in usersData)
        {
            var existingUser = await context.UserAccounts.FirstOrDefaultAsync(x => x.Id == u.Id || x.UserName == u.UserName);
            if (existingUser == null)
            {
                existingUser = new UserAccount
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    NormalizedUserName = u.UserName.ToUpperInvariant(),
                    Email = u.Email,
                    NormalizedEmail = u.Email.ToUpperInvariant(),
                    EmailConfirmed = true,
                    PasswordHash = DefaultPasswordHash,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    Role = u.Role,
                    IsActive = true
                };
                await context.UserAccounts.AddAsync(existingUser);
            }
            else
            {
                existingUser.Email = u.Email;
                existingUser.NormalizedEmail = u.Email.ToUpperInvariant();
                existingUser.PasswordHash = DefaultPasswordHash;
                existingUser.EmailConfirmed = true;
            }

            // Bù đắp UserProfile nếu chưa có
            var existingProfile = await context.UserProfiles.FirstOrDefaultAsync(p => p.Id == u.ProfileId || p.UserAccountId == u.Id);
            if (existingProfile == null)
            {
                var profile = new UserProfile
                {
                    Id = u.ProfileId,
                    UserAccountId = u.Id,
                    FullName = u.FullName,
                    DateOfBirth = u.Dob,
                    Email = u.Email,
                    PhoneNumber = u.Phone,
                    Address = "Hà Nội / TP.HCM",
                    IdentityNumber = u.Identity,
                    AvatarImg = "https://assets.trustsim.vn/avatars/default.png",
                    VerificationStatus = u.Status,
                    IsActive = true
                };
                await context.UserProfiles.AddAsync(profile);
            }
            else
            {
                existingProfile.Email = u.Email;
                existingProfile.VerificationStatus = u.Status;
            }

            // Bù đắp Wallet nếu chưa có
            var existingWallet = await context.Wallets.FirstOrDefaultAsync(w => w.Id == u.WalletId || w.UserAccountId == u.Id);
            if (existingWallet == null)
            {
                var wallet = new Wallet
                {
                    Id = u.WalletId,
                    UserAccountId = u.Id,
                    AvailableBalance = u.Available,
                    LockedBalance = u.Locked,
                    WalletStatus = u.UserName == "user_suspicious_bot" ? WalletStatus.FROZEN : WalletStatus.ACTIVE,
                    LockVersion = u.Locked > 0 ? 1 : 0,
                    IsActive = true
                };
                await context.Wallets.AddAsync(wallet);
            }
            else
            {
                existingWallet.AvailableBalance = u.Available;
                existingWallet.LockedBalance = u.Locked;
                if (u.UserName != "user_suspicious_bot" && existingWallet.WalletStatus == WalletStatus.FROZEN)
                {
                    existingWallet.WalletStatus = WalletStatus.ACTIVE;
                }
            }

            // Bù đắp UserSecurityProfile nếu cần
            if (u.HasPki)
            {
                var existingSec = await context.UserSecurityProfiles.FirstOrDefaultAsync(s => s.Id == u.SecurityId || s.UserAccountId == u.Id);
                if (existingSec == null)
                {
                    var sec = new UserSecurityProfile
                    {
                        Id = u.SecurityId,
                        UserAccountId = u.Id,
                        PublicKey = $"MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA_{u.UserName}...",
                        EncryptedPrivateKey = $"U2FsdGVkX1+v_{u.UserName}_EncryptedPrivateKey...",
                        KeyHash = DefaultPasswordHash,
                        KeyGeneratedAt = DateTime.UtcNow,
                        UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE,
                        IsActive = true
                    };
                    await context.UserSecurityProfiles.AddAsync(sec);
                }
            }

            // NẠP VÀ ĐỒNG BỘ BẢNG user_role (IdentityUserRole<Guid>)
            var roleKey = u.Role.ToString().ToUpperInvariant();
            if (roleDict.TryGetValue(roleKey, out var roleId))
            {
                var hasUserRole = await context.UserRoles.AnyAsync(ur => ur.UserId == u.Id && ur.RoleId == roleId);
                if (!hasUserRole)
                {
                    await context.UserRoles.AddAsync(new IdentityUserRole<Guid>
                    {
                        UserId = u.Id,
                        RoleId = roleId
                    });
                }
            }
        }

        await context.SaveChangesAsync();

        // 3.1 Nạp chứng từ và mắt xích chuỗi khối Sổ cái Kép (Double-Entry Ledger Hash Chain) cho các số dư khởi tạo
        await SeedInitialLedgerJournalsAndEntriesAsync(context, usersData);

        // Identity Verifications
        if (!await context.IdentityVerifications.AnyAsync())
        {
            var adminId = Guid.Parse("11000000-0000-0000-0000-000000000001");
            var profile3 = Guid.Parse("12000000-0000-0000-0000-000000000003");
            var profile8 = Guid.Parse("12000000-0000-0000-0000-000000000008");
            var profile9 = Guid.Parse("12000000-0000-0000-0000-000000000009");

            var ivs = new List<IdentityVerification>
            {
                new()
                {
                    Id = Guid.Parse("15000000-0000-0000-0000-000000000001"),
                    UserProfileId = profile3,
                    VerifiedByUserId = adminId,
                    FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_001.jpg",
                    BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_001.jpg",
                    FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_001.jpg",
                    FaceSimilarityScore = 99.2f,
                    OcrRawData = "{\n  \"extractedInfo\": {\n    \"identityNumber\": \"001085009999\",\n    \"fullName\": \"TRẦN PHÚ QUÝ\",\n    \"dateOfBirth\": \"20/10/1985\",\n    \"gender\": \"Nam\",\n    \"nationality\": \"Việt Nam\",\n    \"placeOfOrigin\": \"Hà Nội\",\n    \"placeOfResidence\": \"Hà Nội\",\n    \"expiryDate\": \"20/10/2035\"\n  },\n  \"confidenceScore\": 99.2,\n  \"totalLinesDetected\": 8,\n  \"detectedLines\": [\n    \"CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\",\n    \"CĂN CƯỚC CÔNG DÂN\",\n    \"Số / No.: 001085009999\",\n    \"Họ và tên / Full name:\",\n    \"TRẦN PHÚ QUÝ\",\n    \"Ngày sinh: 20/10/1985\",\n    \"Giới tính: Nam\",\n    \"Quốc tịch: Việt Nam\"\n  ]\n}",
                    VerificationMethod = "AUTOMATED_AWS_REKOGNITION",
                    VerificationStatus = VerificationStatus.VERIFIED,
                    SubmittedAt = DateTime.UtcNow.AddDays(-10),
                    VerifiedAt = DateTime.UtcNow.AddDays(-10)
                },
                new()
                {
                    Id = Guid.Parse("15000000-0000-0000-0000-000000000002"),
                    UserProfileId = profile8,
                    VerifiedByUserId = null,
                    FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_008.jpg",
                    BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_008.jpg",
                    FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_008.jpg",
                    FaceSimilarityScore = 78.5f,
                    OcrRawData = "{\n  \"extractedInfo\": {\n    \"identityNumber\": \"001098004444\",\n    \"fullName\": \"BÙI THANH HÀ\",\n    \"dateOfBirth\": \"01/12/1998\",\n    \"gender\": \"Nữ\",\n    \"nationality\": \"Việt Nam\",\n    \"placeOfOrigin\": \"Hà Nội\",\n    \"placeOfResidence\": \"Hà Nội\",\n    \"expiryDate\": \"01/12/2038\"\n  },\n  \"confidenceScore\": 78.5,\n  \"totalLinesDetected\": 8,\n  \"detectedLines\": [\n    \"CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\",\n    \"CĂN CƯỚC CÔNG DÂN\",\n    \"Số / No.: 001098004444\",\n    \"Họ và tên / Full name:\",\n    \"BÙI THANH HÀ\",\n    \"Ngày sinh: 01/12/1998\",\n    \"Giới tính: Nữ\",\n    \"Quốc tịch: Việt Nam\"\n  ]\n}",
                    VerificationMethod = "MANUAL_ADMIN_REVIEW",
                    VerificationStatus = VerificationStatus.PENDING_VERIFICATION,
                    SubmittedAt = DateTime.UtcNow.AddHours(-2)
                },
                new()
                {
                    Id = Guid.Parse("15000000-0000-0000-0000-000000000003"),
                    UserProfileId = profile9,
                    VerifiedByUserId = adminId,
                    FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_009.jpg",
                    BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_009.jpg",
                    FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_009.jpg",
                    FaceSimilarityScore = 42.0f,
                    OcrRawData = "{\n  \"extractedInfo\": {\n    \"identityNumber\": \"040095003333\",\n    \"fullName\": \"NGÔ VIỆT CƯỜNG\",\n    \"dateOfBirth\": \"04/04/1995\",\n    \"gender\": \"Nam\",\n    \"nationality\": \"Việt Nam\",\n    \"placeOfOrigin\": \"Nghệ An\",\n    \"placeOfResidence\": \"Nghệ An\",\n    \"expiryDate\": \"04/04/2035\"\n  },\n  \"confidenceScore\": 42.0,\n  \"totalLinesDetected\": 8,\n  \"detectedLines\": [\n    \"CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\",\n    \"CĂN CƯỚC CÔNG DÂN\",\n    \"Số / No.: 040095003333\",\n    \"Họ và tên / Full name:\",\n    \"NGÔ VIỆT CƯỜNG\",\n    \"Ngày sinh: 04/04/1995\",\n    \"Giới tính: Nam\",\n    \"Quốc tịch: Việt Nam\"\n  ]\n}",
                    VerificationMethod = "AUTOMATED_AWS_REKOGNITION",
                    VerificationStatus = VerificationStatus.REJECTED,
                    SubmittedAt = DateTime.UtcNow.AddDays(-3),
                    VerifiedAt = DateTime.UtcNow.AddDays(-3),
                    RejectionReason = "Ảnh chụp khuôn mặt không khớp với ảnh trên CCCD (Độ tương đồng 42% < 85%)"
                }
            };
            await context.IdentityVerifications.AddRangeAsync(ivs);
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  3.1. Level 1: Khởi tạo Chứng từ & Sổ cái Kép (Ledger Hash Chain)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedInitialLedgerJournalsAndEntriesAsync(
        TrustSimDbContext context,
        IEnumerable<dynamic> usersData)
    {
        var signingService = new EcdsaFinancialSigningService(NullLogger<EcdsaFinancialSigningService>.Instance);

        // Tìm mắt xích LedgerEntry cuối cùng trong DB để nối chuỗi tiếp
        var lastEntry = await context.LedgerEntries.OrderByDescending(e => e.SequenceNumber).FirstOrDefaultAsync();
        var currentSequence = lastEntry?.SequenceNumber ?? 0L;
        var currentPrevHash = lastEntry?.EntryHash ?? LedgerHashHelper.GenesisPrevHash;

        foreach (var u in usersData)
        {
            decimal available = u.Available;
            if (available <= 0) continue;

            Guid walletId = u.WalletId;
            string userName = u.UserName;
            string fullName = u.FullName;

            // Kiểm tra xem đã có LedgerEntry nạp tiền khởi tạo cho ví này chưa
            var hasExistingEntries = await context.LedgerEntries.AnyAsync(e => e.WalletId == walletId && e.AccountType == FinancialAccountType.USER_AVAILABLE);
            if (hasExistingEntries) continue;

            var journalId = Guid.NewGuid();
            var journalCode = $"JRN-INIT-{userName.ToUpperInvariant()}";
            var now = new DateTime((DateTime.UtcNow.AddDays(-30).Ticks / 10) * 10, DateTimeKind.Utc);

            var journal = new LedgerJournal
            {
                Id = journalId,
                JournalCode = journalCode,
                TransactionType = LedgerTransactionType.TOP_UP,
                ReferenceType = "SeedInitialBalance",
                ReferenceId = walletId,
                Description = $"Nạp số dư khởi tạo cho ví {fullName} ({userName})",
                TotalAmount = available,
                Status = JournalStatus.POSTED,
                PostedAt = now
            };

            // Bút toán 1: TK 1010 (EXTERNAL_GATEWAY_CLEARING) - DEBIT
            currentSequence++;
            var payload1 = LedgerHashHelper.BuildEntryPayload(
                currentSequence,
                journalId,
                FinancialAccountType.EXTERNAL_GATEWAY_CLEARING,
                LedgerEntryType.DEBIT,
                available,
                null,
                currentPrevHash,
                now);
            var entryHash1 = LedgerHashHelper.ComputeSha256Hex(payload1);
            var sig1 = await signingService.SignHashAsync(entryHash1);

            var entry1 = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                JournalId = journalId,
                WalletId = null,
                AccountType = FinancialAccountType.EXTERNAL_GATEWAY_CLEARING,
                EntryType = LedgerEntryType.DEBIT,
                Amount = available,
                SequenceNumber = currentSequence,
                PrevHash = currentPrevHash,
                EntryHash = entryHash1,
                Signature = sig1,
                CreatedAt = now,
                Description = $"Đối ứng nạp SePay khởi tạo {userName}"
            };
            currentPrevHash = entryHash1;

            // Bút toán 2: TK 2010 (USER_AVAILABLE) - CREDIT
            currentSequence++;
            var payload2 = LedgerHashHelper.BuildEntryPayload(
                currentSequence,
                journalId,
                FinancialAccountType.USER_AVAILABLE,
                LedgerEntryType.CREDIT,
                available,
                walletId,
                currentPrevHash,
                now);
            var entryHash2 = LedgerHashHelper.ComputeSha256Hex(payload2);
            var sig2 = await signingService.SignHashAsync(entryHash2);

            var entry2 = new LedgerEntry
            {
                Id = Guid.NewGuid(),
                JournalId = journalId,
                WalletId = walletId,
                AccountType = FinancialAccountType.USER_AVAILABLE,
                EntryType = LedgerEntryType.CREDIT,
                Amount = available,
                SequenceNumber = currentSequence,
                PrevHash = currentPrevHash,
                EntryHash = entryHash2,
                Signature = sig2,
                CreatedAt = now,
                Description = $"Cộng số dư khả dụng khởi tạo {userName}"
            };
            currentPrevHash = entryHash2;

            // Ký số toàn bộ chứng từ Journal
            var journalPayload = $"{journal.JournalCode}:{journal.TransactionType}:{journal.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)}:{journal.PostedAt.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.ffffffZ}:2";
            journal.SignedHash = await signingService.SignHashAsync(LedgerHashHelper.ComputeSha256Hex(journalPayload));

            journal.Entries.Add(entry1);
            journal.Entries.Add(entry2);

            await context.LedgerJournals.AddAsync(journal);
            await context.LedgerEntries.AddRangeAsync(entry1, entry2);
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  4. Level 2 & 3: Kho SIM, Phân loại & Quyền sở hữu
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedSimsAndOwnershipsAsync(TrustSimDbContext context)
    {
        var defaultPrefix = await context.PhonePrefixes.Select(p => p.Id).FirstOrDefaultAsync();
        var pr098 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "098"))?.Id ?? defaultPrefix;
        var pr097 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "097"))?.Id ?? defaultPrefix;
        var pr086 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "086"))?.Id ?? defaultPrefix;
        var pr033 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "033"))?.Id ?? defaultPrefix;
        var pr091 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "091"))?.Id ?? defaultPrefix;
        var pr088 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "088"))?.Id ?? defaultPrefix;
        var pr090 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "090"))?.Id ?? defaultPrefix;
        var pr079 = (await context.PhonePrefixes.FirstOrDefaultAsync(p => p.PrefixValue == "079"))?.Id ?? defaultPrefix;

        var defaultRhythm = await context.FormatRhythms.Select(r => r.Id).FirstOrDefaultAsync();
        var f22222 = (await context.FormatRhythms.FirstOrDefaultAsync(r => r.Value == "2.2.2.2.2"))?.Id ?? defaultRhythm;
        var f244 = (await context.FormatRhythms.FirstOrDefaultAsync(r => r.Value == "2.4.4"))?.Id ?? defaultRhythm;
        var f28 = (await context.FormatRhythms.FirstOrDefaultAsync(r => r.Value == "2.8"))?.Id ?? defaultRhythm;
        var f46 = (await context.FormatRhythms.FirstOrDefaultAsync(r => r.Value == "4.6"))?.Id ?? defaultRhythm;
        var f334 = (await context.FormatRhythms.FirstOrDefaultAsync(r => r.Value == "3.3.4"))?.Id ?? defaultRhythm;

        var defaultCategory = await context.SimCategories.Select(c => c.Id).FirstOrDefaultAsync();
        var caBatQuy = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "BAT_QUY"))?.Id ?? defaultCategory;
        var caLucQuy = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "LUC_QUY"))?.Id ?? defaultCategory;
        var caTuQuy = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "TU_QUY"))?.Id ?? defaultCategory;
        var caSanhTien = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "SANH_TIEN"))?.Id ?? defaultCategory;
        var caDaiTaxi86 = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "DAI_TAXI_86"))?.Id ?? defaultCategory;
        var caLocPhatKep = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "LOC_PHAT_KEP"))?.Id ?? defaultCategory;
        var caLocPhat86 = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "LOC_PHAT_86"))?.Id ?? defaultCategory;
        var caThanTai = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "THAN_TAI"))?.Id ?? defaultCategory;
        var caThanTaiLon = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "THAN_TAI_LON"))?.Id ?? defaultCategory;
        var caKhongThatBat = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "KHONG_THAT_BAT"))?.Id ?? defaultCategory;
        var caGanhDao = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "GANH_DAO"))?.Id ?? defaultCategory;
        var caLapDoi = (await context.SimCategories.FirstOrDefaultAsync(c => c.CategoryCode == "LAP_DOI"))?.Id ?? defaultCategory;

        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var sellerDaiPhat = Guid.Parse("11000000-0000-0000-0000-000000000004");

        var simDefinitions = new[]
        {
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000001"), Phone = "0988888888", Price = 1800000000m, Status = SimStatus.IN_AUCTION, Prefix = pr098, Rhythm = f28, Seller = sellerPhuQuy, Categories = new[] { caBatQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000002"), Phone = "0909999999", Price = 550000000m,  Status = SimStatus.TRANSFERRED, Prefix = pr090, Rhythm = f46, Seller = sellerPhuQuy, Categories = new[] { caLucQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000003"), Phone = "0912345678", Price = 105000000m,  Status = SimStatus.IN_AUCTION, Prefix = pr091, Rhythm = f28, Seller = sellerDaiPhat, Categories = new[] { caSanhTien } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000004"), Phone = "0888686868", Price = 72000000m,   Status = SimStatus.IN_AUCTION, Prefix = pr088, Rhythm = f22222, Seller = sellerDaiPhat, Categories = new[] { caLocPhatKep, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000005"), Phone = "0797979799", Price = 35000000m,   Status = SimStatus.AVAILABLE,  Prefix = pr079, Rhythm = f22222, Seller = sellerPhuQuy, Categories = new[] { caThanTaiLon } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000006"), Phone = "0966668888", Price = 120000000m,  Status = SimStatus.TRANSFERRED, Prefix = pr097, Rhythm = f244, Seller = sellerPhuQuy, Categories = new[] { caTuQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000007"), Phone = "0986868686", Price = 2200000000m, Status = SimStatus.IN_AUCTION, Prefix = pr098, Rhythm = f22222, Seller = sellerPhuQuy, Categories = new[] { caDaiTaxi86, caLocPhat86, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000008"), Phone = "0869192776", Price = 450000m,       Status = SimStatus.AVAILABLE,  Prefix = pr086, Rhythm = f334, Seller = sellerDaiPhat, Categories = new[] { caGanhDao } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000009"), Phone = "0903123678", Price = 2500000m,     Status = SimStatus.AVAILABLE,  Prefix = pr090, Rhythm = f334, Seller = sellerDaiPhat, Categories = new[] { caSanhTien } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000010"), Phone = "0977889900", Price = 8500000m,     Status = SimStatus.AVAILABLE,  Prefix = pr097, Rhythm = f22222, Seller = sellerPhuQuy, Categories = new[] { caLapDoi } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000011"), Phone = "0339393939", Price = 18000000m,    Status = SimStatus.AVAILABLE,  Prefix = pr033, Rhythm = f22222, Seller = sellerPhuQuy, Categories = new[] { caThanTai } },
            
            // SIMs mới thêm có sẵn (AVAILABLE) & đã xác minh đầy đủ cho 2 Seller để test tạo phiên đấu giá
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000012"), Phone = "0989999999", Price = 1200000000m, Status = SimStatus.AVAILABLE,  Prefix = pr098, Rhythm = f46,   Seller = sellerPhuQuy, Categories = new[] { caLucQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000013"), Phone = "0908888888", Price = 1500000000m, Status = SimStatus.AVAILABLE,  Prefix = pr090, Rhythm = f46,   Seller = sellerPhuQuy, Categories = new[] { caLucQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000014"), Phone = "0918888999", Price = 95000000m,   Status = SimStatus.AVAILABLE,  Prefix = pr091, Rhythm = f334,  Seller = sellerDaiPhat, Categories = new[] { caThanTaiLon } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000015"), Phone = "0886668888", Price = 180000000m,  Status = SimStatus.AVAILABLE,  Prefix = pr088, Rhythm = f244,  Seller = sellerDaiPhat, Categories = new[] { caTuQuy, caLocPhat86 } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000016"), Phone = "0799998888", Price = 250000000m,  Status = SimStatus.AVAILABLE,  Prefix = pr079, Rhythm = f244,  Seller = sellerPhuQuy, Categories = new[] { caTuQuy, caThanTaiLon } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000017"), Phone = "0338889999", Price = 75000000m,   Status = SimStatus.AVAILABLE,  Prefix = pr033, Rhythm = f244,  Seller = sellerDaiPhat, Categories = new[] { caTuQuy, caLocPhat86 } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000018"), Phone = "0868888888", Price = 990000000m,  Status = SimStatus.AVAILABLE,  Prefix = pr086, Rhythm = f28,   Seller = sellerPhuQuy, Categories = new[] { caBatQuy, caKhongThatBat } },
            new { Id = Guid.Parse("21000000-0000-0000-0000-000000000019"), Phone = "0987654321", Price = 350000000m,  Status = SimStatus.AVAILABLE,  Prefix = pr098, Rhythm = f28,   Seller = sellerDaiPhat, Categories = new[] { caSanhTien } }
        };

        foreach (var def in simDefinitions)
        {
            var sim = await context.SimNumbers.FirstOrDefaultAsync(s => s.PhoneNumber == def.Phone);
            if (sim == null)
            {
                sim = new SimNumber
                {
                    Id = def.Id,
                    PhoneNumber = def.Phone,
                    Price = def.Price,
                    SimStatus = def.Status,
                    PhonePrefixId = def.Prefix,
                    FormatRhythmId = def.Rhythm
                };
                await context.SimNumbers.AddAsync(sim);
            }
            else
            {
                sim.SimStatus = def.Status;
                sim.Price = def.Price;
            }

            foreach (var catId in def.Categories)
            {
                var hasCat = await context.SimCategoryMappings.AnyAsync(m => m.SimNumberId == sim.Id && m.SimCategoryId == catId);
                if (!hasCat)
                {
                    await context.SimCategoryMappings.AddAsync(new SimCategoryMapping
                    {
                        Id = Guid.NewGuid(),
                        SimNumberId = sim.Id,
                        SimCategoryId = catId,
                        ScoreContribution = 1.0f
                    });
                }
            }

            var ownership = await context.SimOwnerships.FirstOrDefaultAsync(o => o.SimNumberId == sim.Id && o.UserAccountId == def.Seller);
            if (ownership == null)
            {
                ownership = new SimOwnership
                {
                    Id = Guid.NewGuid(),
                    UserAccountId = def.Seller,
                    SimNumberId = sim.Id,
                    OwnershipStatus = OwnershipStatus.ACTIVE,
                    AcquistionType = AcquistionType.INITIAL_VERIFICATION
                };
                await context.SimOwnerships.AddAsync(ownership);
            }
            else
            {
                ownership.OwnershipStatus = OwnershipStatus.ACTIVE;
            }

            var verification = await context.SimVerifications.FirstOrDefaultAsync(v => v.SimOwnershipId == ownership.Id);
            if (verification == null)
            {
                verification = new SimVerification
                {
                    Id = Guid.NewGuid(),
                    SimOwnershipId = ownership.Id,
                    VerificationStatus = VerificationStatus.VERIFIED,
                    RequestedAt = DateTime.UtcNow.AddDays(-10),
                    VerifiedAt = DateTime.UtcNow.AddDays(-10)
                };
                await context.SimVerifications.AddAsync(verification);
            }
            else
            {
                verification.VerificationStatus = VerificationStatus.VERIFIED;
            }
        }

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  5. Level 3 & 4: Tri thức RAG & Lịch sử Định giá AI
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAiValuationAndRagDocumentsAsync(TrustSimDbContext context, IServiceProvider? serviceProvider = null)
    {
        // 1. Documents Kho tri thức RAG (d1000000-...)
        var doc1Id = Guid.Parse("d1000000-0000-0000-0000-000000000001");
        var doc2Id = Guid.Parse("d1000000-0000-0000-0000-000000000002");

        var ragDocs = new List<Document>
        {
            new()
            {
                Id = doc1Id,
                DocumentName = "Định Giá Sim Cho AI",
                LinkUrl = "https://docs.google.com/document/d/1_dvURkshUfTtUMLzpLTX-HwTqhT3GNVBqZ5yOQPBK_c/edit?tab=t.0",
                DocumentType = DocumentType.PRICING_GUIDELINE,
                Status = DocumentStatus.PENDING
            },
            new()
            {
                Id = doc2Id,
                DocumentName = "Tài Liệu RAG Sim Phong Thủy",
                LinkUrl = "https://docs.google.com/document/d/1thrdHxxrJZANJoRg_mKY0C9daWMKh1YxCTTDFIpcgtc/edit?tab=t.0",
                DocumentType = DocumentType.FENG_SHUI_RULE,
                Status = DocumentStatus.PENDING
            }
        };

        foreach (var doc in ragDocs)
        {
            var existing = await context.Documents.FirstOrDefaultAsync(d => d.Id == doc.Id);
            if (existing == null)
            {
                await context.Documents.AddAsync(doc);
            }
            else
            {
                existing.DocumentName = doc.DocumentName;
                existing.LinkUrl = doc.LinkUrl;
                existing.DocumentType = doc.DocumentType;
            }
        }
        await context.SaveChangesAsync();

        // 2. Tự động kích hoạt Ingestion Pipeline thực tế (Crawler -> Text Chunker -> Gemini Embeddings -> PostgreSQL)
        if (serviceProvider != null)
        {
            try
            {
                var ingestionService = serviceProvider.GetService<IDocumentIngestionService>();
                if (ingestionService != null)
                {
                    var needsDoc1 = !await context.DocumentChunks.AnyAsync(c => c.DocumentId == doc1Id);
                    if (needsDoc1)
                    {
                        await ingestionService.IngestDocumentAsync(doc1Id, CancellationToken.None);
                    }

                    var needsDoc2 = !await context.DocumentChunks.AnyAsync(c => c.DocumentId == doc2Id);
                    if (needsDoc2)
                    {
                        await ingestionService.IngestDocumentAsync(doc2Id, CancellationToken.None);
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi kết nối mạng trong quá trình seed khởi động nếu không có internet
            }
        }

        // 3. SimValuations Mẫu (v1000000-...)
        if (!await context.SimValuations.AnyAsync())
        {
            var sim1Id = Guid.Parse("21000000-0000-0000-0000-000000000001"); // 0988888888
            var sim2Id = Guid.Parse("21000000-0000-0000-0000-000000000002"); // 0909999999
            var sim3Id = Guid.Parse("21000000-0000-0000-0000-000000000003"); // 0912345678
            var sim4Id = Guid.Parse("21000000-0000-0000-0000-000000000004"); // 0888686868
            var sim5Id = Guid.Parse("21000000-0000-0000-0000-000000000005"); // 0797979799
            var sim6Id = Guid.Parse("21000000-0000-0000-0000-000000000007"); // 0986868686

            var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
            var sellerDaiPhat = Guid.Parse("11000000-0000-0000-0000-000000000004");

            var valuations = new List<SimValuation>
            {
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000001"),
                    SimNumberId = sim1Id,
                    UserAccountId = sellerPhuQuy,
                    BaselinePrice = 1500000000m,
                    MarketMultiplier = 1.20f,
                    SuggestedPrice = 1800000000m,
                    MinPrice = 1500000000m,
                    MaxPrice = 2200000000m,
                    ConfidenceScore = 0.98f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0988888888 là siêu phẩm Bát Quý 8 thuộc đầu số cổ 098 của Viettel Telecom. Số 8 tượng trưng cho Bát Bạch Phát Tài, mang lại quyền uy và vượng khí tột bậc cho chủ nhân.",
                    FengShuiAnalysis = "Quẻ đại cát, thuộc hành Thổ tương sinh với mệnh Kim và Thổ. Dãy số thuần dương 8 số 8 tạo dòng chảy tài lộc vô tận.",
                    AppliedToSim = true
                },
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000002"),
                    SimNumberId = sim2Id,
                    UserAccountId = sellerPhuQuy,
                    BaselinePrice = 480000000m,
                    MarketMultiplier = 1.15f,
                    SuggestedPrice = 550000000m,
                    MinPrice = 480000000m,
                    MaxPrice = 650000000m,
                    ConfidenceScore = 0.95f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0909999999 là dòng SIM Lục quý 9 đầu gánh 0909 của MobiFone. Số 9 tượng trưng cho Cửu Ngũ Chí Tôn, sự trường thọ và vĩnh cửu.",
                    FengShuiAnalysis = "Hành Hỏa vượng, tương sinh xuất sắc với người mang mệnh Thổ và mệnh Hỏa.",
                    AppliedToSim = true
                },
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000003"),
                    SimNumberId = sim3Id,
                    UserAccountId = sellerDaiPhat,
                    BaselinePrice = 85000000m,
                    MarketMultiplier = 1.25f,
                    SuggestedPrice = 105000000m,
                    MinPrice = 85000000m,
                    MaxPrice = 130000000m,
                    ConfidenceScore = 0.92f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0912345678 sở hữu chuỗi Sảnh Tiến Rồng 8 số liên tiếp (12345678) kết hợp đầu số cổ 091 của VinaPhone, biểu tượng cho sự nghiệp thăng quan tiến chức.",
                    FengShuiAnalysis = "Dãy số tiến liên hoàn mang trường năng lượng phát triển mạnh mẽ và liên tục cho gia chủ kinh doanh.",
                    AppliedToSim = true
                },
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000004"),
                    SimNumberId = sim4Id,
                    UserAccountId = sellerDaiPhat,
                    BaselinePrice = 65000000m,
                    MarketMultiplier = 1.10f,
                    SuggestedPrice = 72000000m,
                    MinPrice = 60000000m,
                    MaxPrice = 85000000m,
                    ConfidenceScore = 0.90f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0888686868 mang đầu số Vina Song Phát 0888 kết hợp đuôi Taxi Lộc Phát kép 686868, dòng số hotline đỉnh cao cho doanh nghiệp thương mại.",
                    FengShuiAnalysis = "Sự phối hợp giữa các chữ số 6 (Lộc) và 8 (Phát) tạo nên thế trận chiêu tài hút lộc không ngừng.",
                    AppliedToSim = false
                },
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000005"),
                    SimNumberId = sim5Id,
                    UserAccountId = sellerPhuQuy,
                    BaselinePrice = 32000000m,
                    MarketMultiplier = 1.10f,
                    SuggestedPrice = 35000000m,
                    MinPrice = 30000000m,
                    MaxPrice = 42000000m,
                    ConfidenceScore = 0.88f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0797979799 thuộc dòng Taxi Thần Tài Lớn 7979799 của MobiFone, mang ý nghĩa Thần tài luôn đồng hành và bảo hộ trong mọi dự án đầu tư.",
                    FengShuiAnalysis = "Số 7 và 9 mang năng lượng Thần tài phù trợ thịnh vượng, hanh thông trong giao dịch mua bán.",
                    AppliedToSim = false
                },
                new()
                {
                    Id = Guid.Parse("e1000000-0000-0000-0000-000000000006"),
                    SimNumberId = sim6Id,
                    UserAccountId = sellerPhuQuy,
                    BaselinePrice = 1850000000m,
                    MarketMultiplier = 1.189f,
                    SuggestedPrice = 2200000000m,
                    MinPrice = 1900000000m,
                    MaxPrice = 2500000000m,
                    ConfidenceScore = 0.99f,
                    AiModelName = "deepseek-ai/deepseek-v4-flash-0731",
                    ValuationReasoning = "SIM 0986868686 đạt đẳng cấp Siêu VIP Đại Taxi 4 cặp Lộc Phát 86 liên tiếp, thuộc đầu số cổ 098 Viettel Telecom. Khi áp dụng nhịp tối ưu 2.2.2.2.2 (09.86.86.86.86), số điện thoại bộc lộ trọn vẹn 4 thế Phát Lộc kép, hoàn toàn sạch số 4 và 7 (Không Thất Bát). So với nhịp mặc định 3.3.4 (098.686.8686 làm đứt đoạn cặp 86), nhịp 2.2.2.2.2 giúp nâng tầm giá trị SIM vượt trội lên mức 2.2 tỷ VND.",
                    FengShuiAnalysis = "Sự lặp lại hoàn hảo của 4 cặp số 86 (Bát Lộc) trên nền đầu số cổ 098 tạo nên thế phong thủy Đắc Lộc Sinh Tiền vĩnh cửu. Dãy số tương sinh vượng khí tối đa cho gia chủ kinh doanh.",
                    AppliedToSim = true
                }
            };

            await context.SimValuations.AddRangeAsync(valuations);
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  6. Level 4 & 5: Các phiên đấu giá, Bids & Gian lận
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAuctionsAndBidsAsync(TrustSimDbContext context)
    {
        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var sellerDaiPhat = Guid.Parse("11000000-0000-0000-0000-000000000004");
        var bidderDaiGia = Guid.Parse("11000000-0000-0000-0000-000000000005");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");

        var sim01 = Guid.Parse("21000000-0000-0000-0000-000000000001"); // 0988888888 (auc1 - English Schedule)
        var sim03 = Guid.Parse("21000000-0000-0000-0000-000000000003"); // 0912345678 (auc2 - Dutch Schedule)
        var sim07 = Guid.Parse("21000000-0000-0000-0000-000000000007"); // 0986868686 (auc3 - English Active)
        var sim04 = Guid.Parse("21000000-0000-0000-0000-000000000004"); // 0888686868 (auc4 - Dutch Active)
        var sim02 = Guid.Parse("21000000-0000-0000-0000-000000000002"); // 0909999999 (auc5 - English End)

        // 1. AUC1: English - SCHEDULE (Chưa bắt đầu, mở đăng ký đặt cọc)
        var auc1 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000001"));
        if (auc1 == null)
        {
            auc1 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000001"),
                AuctionCode = "AUC-20260901-001",
                AuctionType = AuctionType.ENGLISH,
                AuctionStatus = AuctionStatus.SCHEDULE,
                StartingPrice = 500000000m,
                CurrentPrice = 500000000m,
                ReservePrice = 600000000m,
                StartAt = DateTime.UtcNow.AddMinutes(15),
                EndAt = DateTime.UtcNow.AddHours(4),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-1),
                RegistrationEndAt = DateTime.UtcNow.AddMinutes(15),
                LockVersion = 1,
                AuthorId = sellerPhuQuy,
                SimNumberId = sim01
            };
            await context.Auctions.AddAsync(auc1);
        }
        else
        {
            auc1.AuctionStatus = AuctionStatus.SCHEDULE;
            auc1.StartingPrice = 500000000m;
            auc1.CurrentPrice = 500000000m;
            auc1.StartAt = DateTime.UtcNow.AddMinutes(15);
            auc1.EndAt = DateTime.UtcNow.AddHours(4);
            auc1.RegistrationStartAt = DateTime.UtcNow.AddDays(-1);
            auc1.RegistrationEndAt = DateTime.UtcNow.AddMinutes(15);
        }

        // 2. AUC2: Dutch - SCHEDULE (Chưa bắt đầu, mở đăng ký đặt cọc)
        var auc2 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000002"));
        if (auc2 == null)
        {
            auc2 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000002"),
                AuctionCode = "AUC-20260905-002",
                AuctionType = AuctionType.DUTCH,
                AuctionStatus = AuctionStatus.SCHEDULE,
                StartingPrice = 100000000m,
                CurrentPrice = 100000000m,
                ReservePrice = 50000000m,
                StartAt = DateTime.UtcNow.AddMinutes(10),
                EndAt = DateTime.UtcNow.AddHours(2),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-1),
                RegistrationEndAt = DateTime.UtcNow.AddMinutes(10),
                LockVersion = 1,
                AuthorId = sellerDaiPhat,
                SimNumberId = sim03
            };
            await context.Auctions.AddAsync(auc2);
        }
        else
        {
            auc2.AuctionStatus = AuctionStatus.SCHEDULE;
            auc2.StartingPrice = 100000000m;
            auc2.CurrentPrice = 100000000m;
            auc2.StartAt = DateTime.UtcNow.AddMinutes(10);
            auc2.EndAt = DateTime.UtcNow.AddHours(2);
            auc2.RegistrationStartAt = DateTime.UtcNow.AddDays(-1);
            auc2.RegistrationEndAt = DateTime.UtcNow.AddMinutes(10);
        }

        // 3. AUC3: English - SCHEDULE (Phiên đấu giá kiểu Anh, mở đăng ký đặt cọc)
        var auc3 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000003"));
        if (auc3 == null)
        {
            auc3 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000003"),
                AuctionCode = "AUC-20260901-003",
                AuctionType = AuctionType.ENGLISH,
                AuctionStatus = AuctionStatus.SCHEDULE,
                StartingPrice = 1500000000m,
                CurrentPrice = 1500000000m,
                ReservePrice = 1800000000m,
                StartAt = DateTime.UtcNow.AddMinutes(20),
                EndAt = DateTime.UtcNow.AddHours(3),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-2),
                RegistrationEndAt = DateTime.UtcNow.AddMinutes(20),
                LockVersion = 1,
                AuthorId = sellerPhuQuy,
                SimNumberId = sim07
            };
            await context.Auctions.AddAsync(auc3);
        }
        else
        {
            auc3.AuctionCode = "AUC-20260901-003";
            auc3.AuctionType = AuctionType.ENGLISH;
            auc3.AuctionStatus = AuctionStatus.SCHEDULE;
            auc3.StartingPrice = 1500000000m;
            auc3.CurrentPrice = 1500000000m;
            auc3.StartAt = DateTime.UtcNow.AddMinutes(20);
            auc3.EndAt = DateTime.UtcNow.AddHours(3);
            auc3.RegistrationStartAt = DateTime.UtcNow.AddDays(-2);
            auc3.RegistrationEndAt = DateTime.UtcNow.AddMinutes(20);
            auc3.SimNumberId = sim07;
        }

        // 4. AUC4: Dutch - SCHEDULE (Phiên đấu giá giảm giá kiểu Hà Lan, mở đăng ký đặt cọc)
        var auc4 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000004"));
        if (auc4 == null)
        {
            auc4 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000004"),
                AuctionCode = "AUC-20260905-004",
                AuctionType = AuctionType.DUTCH,
                AuctionStatus = AuctionStatus.SCHEDULE,
                StartingPrice = 80000000m,
                CurrentPrice = 80000000m,
                ReservePrice = 40000000m,
                StartAt = DateTime.UtcNow.AddMinutes(25),
                EndAt = DateTime.UtcNow.AddHours(2),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-2),
                RegistrationEndAt = DateTime.UtcNow.AddMinutes(25),
                LockVersion = 1,
                AuthorId = sellerDaiPhat,
                SimNumberId = sim04
            };
            await context.Auctions.AddAsync(auc4);
        }
        else
        {
            auc4.AuctionStatus = AuctionStatus.SCHEDULE;
            auc4.StartingPrice = 80000000m;
            auc4.CurrentPrice = 80000000m;
            auc4.StartAt = DateTime.UtcNow.AddMinutes(25);
            auc4.EndAt = DateTime.UtcNow.AddHours(2);
            auc4.RegistrationStartAt = DateTime.UtcNow.AddDays(-2);
            auc4.RegistrationEndAt = DateTime.UtcNow.AddMinutes(25);
            auc4.SimNumberId = sim04;
        }

        // 5. AUC5: English - END (Phiên đã xong trong quá khứ liên kết TradeTransaction)
        var auc5 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000005"));
        if (auc5 == null)
        {
            auc5 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000005"),
                AuctionCode = "AUC-20260828-005",
                AuctionType = AuctionType.ENGLISH,
                AuctionStatus = AuctionStatus.END,
                StartingPrice = 150000000m,
                CurrentPrice = 210000000m,
                ReservePrice = 180000000m,
                StartAt = DateTime.UtcNow.AddDays(-5),
                EndAt = DateTime.UtcNow.AddDays(-2),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-8),
                RegistrationEndAt = DateTime.UtcNow.AddDays(-5),
                LockVersion = 5,
                AuthorId = sellerPhuQuy,
                SimNumberId = sim02
            };
            await context.Auctions.AddAsync(auc5);
        }

        // 6. AUC6: English - END (Phiên vừa kết thúc thành công, quỹ Escrow đã nạp, đang Chờ Ký Hợp Đồng)
        var sim06 = Guid.Parse("21000000-0000-0000-0000-000000000006"); // 0966668888
        var auc6 = await context.Auctions.FirstOrDefaultAsync(a => a.Id == Guid.Parse("31000000-0000-0000-0000-000000000006"));
        if (auc6 == null)
        {
            auc6 = new Auction
            {
                Id = Guid.Parse("31000000-0000-0000-0000-000000000006"),
                AuctionCode = "AUC-20261010-006",
                AuctionType = AuctionType.ENGLISH,
                AuctionStatus = AuctionStatus.END,
                StartingPrice = 100000000m,
                CurrentPrice = 120000000m,
                ReservePrice = 100000000m,
                StartAt = DateTime.UtcNow.AddHours(-3),
                EndAt = DateTime.UtcNow.AddMinutes(-30),
                RegistrationStartAt = DateTime.UtcNow.AddDays(-1),
                RegistrationEndAt = DateTime.UtcNow.AddHours(-3),
                LockVersion = 3,
                AuthorId = sellerPhuQuy,
                SimNumberId = sim06
            };
            await context.Auctions.AddAsync(auc6);
        }

        await context.SaveChangesAsync();

        // 2. Auction Rules (32000000-...)
        var rule1 = await context.AuctionRules.FirstOrDefaultAsync(r => r.AuctionId == auc1.Id);
        if (rule1 == null)
        {
            rule1 = new AuctionRule
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000001"),
                AuctionId = auc1.Id,
                MinimumBidIncrement = 10000000m,
                DepositAmount = 50000000m,
                SellerDepositAmount = 25000000m,
                SellerDepositRate = 0.05m,
                SigningGracePeriodHours = 24,
                SigningGracePeriodMinutes = 1440,
                MaxStepMultiplier = 10,
                MaxSnipeExtensionsPerParticipant = 3,
                PaymentGracePeriodHours = 24,
                MaximumBidCountPerParticipant = 50,
                MaximumBidCountPerMinute = 10,
                MinimumBidIntervalSeconds = 1.0f,
                ExtensionThresholdSeconds = 120.0f,
                ExtensionDurationSeconds = 300.0f,
                MaximumExtensionCount = 5
            };
            await context.AuctionRules.AddAsync(rule1);
        }

        var rule2 = await context.AuctionRules.FirstOrDefaultAsync(r => r.AuctionId == auc2.Id);
        if (rule2 == null)
        {
            rule2 = new AuctionRule
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000002"),
                AuctionId = auc2.Id,
                MinimumBidIncrement = 0m,
                DepositAmount = 10000000m,
                SellerDepositAmount = 5000000m,
                SellerDepositRate = 0.05m,
                SigningGracePeriodHours = 24,
                SigningGracePeriodMinutes = 1440,
                PaymentGracePeriodHours = 24,
                MaximumBidCountPerParticipant = 1,
                MaximumBidCountPerMinute = 1,
                MinimumBidIntervalSeconds = 0.0f,
                DutchDecreaseAmount = 2000000m,
                DutchDecreaseIntervalSeconds = 60.0f
            };
            await context.AuctionRules.AddAsync(rule2);
        }

        var rule3 = await context.AuctionRules.FirstOrDefaultAsync(r => r.AuctionId == auc3.Id);
        if (rule3 == null)
        {
            rule3 = new AuctionRule
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000003"),
                AuctionId = auc3.Id,
                MinimumBidIncrement = 50000000m,
                DepositAmount = 150000000m,
                SellerDepositAmount = 75000000m,
                SellerDepositRate = 0.05m,
                SigningGracePeriodHours = 24,
                SigningGracePeriodMinutes = 1440,
                MaxStepMultiplier = 10,
                MaxSnipeExtensionsPerParticipant = 3,
                PaymentGracePeriodHours = 24,
                MaximumBidCountPerParticipant = 50,
                MaximumBidCountPerMinute = 10,
                MinimumBidIntervalSeconds = 1.0f,
                ExtensionThresholdSeconds = 120.0f,
                ExtensionDurationSeconds = 300.0f,
                MaximumExtensionCount = 5
            };
            await context.AuctionRules.AddAsync(rule3);
        }

        var rule4 = await context.AuctionRules.FirstOrDefaultAsync(r => r.AuctionId == auc4.Id);
        if (rule4 == null)
        {
            rule4 = new AuctionRule
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000004"),
                AuctionId = auc4.Id,
                MinimumBidIncrement = 0m,
                DepositAmount = 8000000m,
                SellerDepositAmount = 4000000m,
                SellerDepositRate = 0.05m,
                SigningGracePeriodHours = 24,
                SigningGracePeriodMinutes = 1440,
                PaymentGracePeriodHours = 24,
                MaximumBidCountPerParticipant = 1,
                MaximumBidCountPerMinute = 1,
                MinimumBidIntervalSeconds = 0.0f,
                DutchDecreaseAmount = 2000000m,
                DutchDecreaseIntervalSeconds = 60.0f
            };
            await context.AuctionRules.AddAsync(rule4);
        }

        var rule6 = await context.AuctionRules.FirstOrDefaultAsync(r => r.AuctionId == auc6.Id);
        if (rule6 == null)
        {
            rule6 = new AuctionRule
            {
                Id = Guid.Parse("32000000-0000-0000-0000-000000000006"),
                AuctionId = auc6.Id,
                MinimumBidIncrement = 10000000m,
                DepositAmount = 10000000m,
                SellerDepositAmount = 5000000m,
                SellerDepositRate = 0.05m,
                SigningGracePeriodHours = 24,
                SigningGracePeriodMinutes = 1440,
                MaxStepMultiplier = 10,
                MaxSnipeExtensionsPerParticipant = 3,
                PaymentGracePeriodHours = 24,
                MaximumBidCountPerParticipant = 50,
                MaximumBidCountPerMinute = 10,
                MinimumBidIntervalSeconds = 1.0f
            };
            await context.AuctionRules.AddAsync(rule6);
        }

        // Dọn sạch toàn bộ participants, bids, deposits, results cũ khỏi tất cả các phiên test (auc1, auc2, auc3, auc4)
        // để tester tự do thực hiện luồng đặt cọc & trả giá từ đầu bằng bất kỳ tài khoản nào
        var testAucIds = new[] { auc1.Id, auc2.Id, auc3.Id, auc4.Id };
        var staleBids = await context.Bids.Where(b => testAucIds.Contains(b.AuctionId)).ToListAsync();
        if (staleBids.Count > 0) context.Bids.RemoveRange(staleBids);

        var staleDeposits = await context.AuctionDeposits.Where(d => d.AuctionParticipant != null && testAucIds.Contains(d.AuctionParticipant.AuctionId)).ToListAsync();
        if (staleDeposits.Count > 0) context.AuctionDeposits.RemoveRange(staleDeposits);

        var staleParticipants = await context.AuctionParticipants.Where(p => testAucIds.Contains(p.AuctionId)).ToListAsync();
        if (staleParticipants.Count > 0) context.AuctionParticipants.RemoveRange(staleParticipants);

        var staleResults = await context.AuctionResults.Where(r => testAucIds.Contains(r.AuctionId)).ToListAsync();
        if (staleResults.Count > 0) context.AuctionResults.RemoveRange(staleResults);

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  7. Level 4 -> 6: Giao dịch chuyển nhượng, Hợp đồng & Ký quỹ
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedTradeAndContractsAsync(TrustSimDbContext context)
    {
        if (await context.TradeTransactions.AnyAsync()) return;

        var auc5Id = Guid.Parse("31000000-0000-0000-0000-000000000005");
        var auc6Id = Guid.Parse("31000000-0000-0000-0000-000000000006");
        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var bidderDaiGia = Guid.Parse("11000000-0000-0000-0000-000000000005");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");
        var sim02 = Guid.Parse("21000000-0000-0000-0000-000000000002"); // 0909999999
        var sim06 = Guid.Parse("21000000-0000-0000-0000-000000000006"); // 0966668888

        // ══════════════════════════════════════════════════════════════
        // GIAO DỊCH 1: TRD-20260830-001 (ĐÃ KÝ XONG - FULLY_SIGNED)
        // Dùng test Tab 2 (Xem/Tải PDF) và Tab 3 (Kiểm định SHA-256 PAdES)
        // ══════════════════════════════════════════════════════════════
        var trade1 = new TradeTransaction
        {
            Id = Guid.Parse("41000000-0000-0000-0000-000000000001"),
            TradeTransactionCode = "TRD-20260830-001",
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
            EscrowDeadline = DateTime.UtcNow.AddDays(1),
            SigningDeadline = DateTime.UtcNow.AddDays(2),
            TransferDeadline = DateTime.UtcNow.AddDays(5),
            CompletedAt = DateTime.UtcNow,
            AuctionId = auc5Id,
            BuyerId = bidderKinhDoanh,
            SellerId = sellerPhuQuy,
            SimNumberId = sim02
        };
        await context.TradeTransactions.AddAsync(trade1);

        var escrow1 = new Escrow
        {
            Id = Guid.Parse("42000000-0000-0000-0000-000000000001"),
            EscrowAmount = 210000000m,
            EscrowStatus = EscrowStatus.HELD,
            HeldAt = DateTime.UtcNow.AddDays(-2),
            AuctionDepositId = null,
            TradeTransactionId = trade1.Id
        };
        await context.Escrows.AddAsync(escrow1);

        var contract1 = new TransferContract
        {
            Id = Guid.Parse("43000000-0000-0000-0000-000000000001"),
            DocumentUrl = "https://storage.trustsim.vn/contracts/TRD-20260830-001.pdf",
            DocumentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            BlockchainTxHash = "0x7f9fad1c0e5a83b275c4e976ec4e0626a23531b79f83526d11f71a938c0f5f84",
            ContractStatus = ContractStatus.FULLY_SIGNED,
            PadesSignatureSerial = "4A8F9C1288B1",
            PadesSignatureHash = "7f9fad1c0e5a83b275c4e976ec4e0626a23531b79f83526d11f71a938c0f5f84",
            TsaTimestamp = DateTime.UtcNow.AddDays(-1),
            TsaAuthority = "freetsa.org",
            SealedAt = DateTime.UtcNow.AddDays(-1),
            IsAutoEnforced = false,
            TradeTransactionId = trade1.Id
        };
        await context.TransferContracts.AddAsync(contract1);

        var sigSeller = new DigitalSignature
        {
            Id = Guid.Parse("44000000-0000-0000-0000-000000000001"),
            SignatureRole = SignatureRole.SELLER,
            AuthMethod = "EMAIL_OTP",
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0 TrustSim-Seeder",
            OtpCodeHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            OtpVerifiedAt = DateTime.UtcNow.AddDays(-1),
            SignedAt = DateTime.UtcNow.AddDays(-1),
            AuditMetadataJson = "{\"role\":\"SELLER\",\"method\":\"EMAIL_OTP\",\"consent\":true}",
            PublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA3003...",
            CryptographicSignature = "KycSig_Seller_PhuQuy_RSA_Signature_Hash_Value_001...",
            TransferContractId = contract1.Id,
            UserId = sellerPhuQuy
        };

        var sigBuyer = new DigitalSignature
        {
            Id = Guid.Parse("44000000-0000-0000-0000-000000000002"),
            SignatureRole = SignatureRole.BUYER,
            AuthMethod = "EMAIL_OTP",
            IpAddress = "127.0.0.1",
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/120.0 TrustSim-Seeder",
            OtpCodeHash = "6b86b273ff34fce19d6b804eff5a3f5747ada4eaa22f1d49c01e52ddb7875b4b",
            OtpVerifiedAt = DateTime.UtcNow.AddHours(-22),
            SignedAt = DateTime.UtcNow.AddHours(-22),
            AuditMetadataJson = "{\"role\":\"BUYER\",\"method\":\"EMAIL_OTP\",\"consent\":true}",
            PublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6006...",
            CryptographicSignature = "KycSig_Buyer_MinhTri_RSA_Signature_Hash_Value_002...",
            TransferContractId = contract1.Id,
            UserId = bidderKinhDoanh
        };
        await context.DigitalSignatures.AddRangeAsync(sigSeller, sigBuyer);

        var transfer = new SimTransfer
        {
            Id = Guid.Parse("45000000-0000-0000-0000-000000000001"),
            StartAt = DateTime.UtcNow.AddDays(-1),
            Deadline = DateTime.UtcNow.AddDays(3),
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED,
            TradeTransactionId = trade1.Id
        };
        await context.SimTransfers.AddAsync(transfer);

        var proof = new TransferProof
        {
            Id = Guid.Parse("46000000-0000-0000-0000-000000000001"),
            ImageUrl = "https://storage.trustsim.vn/proofs/mobifone_transfer_0909999999.jpg",
            Description = "Phiếu tiếp nhận sang tên chính chủ tại cửa hàng MobiFone 84 Bà Triệu",
            ProofStatus = ProofStatus.APPROVED,
            VerifiedAt = DateTime.UtcNow.AddHours(-10),
            SimTransferId = transfer.Id
        };
        await context.TransferProofs.AddAsync(proof);

        // ══════════════════════════════════════════════════════════════
        // GIAO DỊCH 2: TRD-20261010-002 (CHỜ KÝ SỐ - PendingSignature)
        // Dùng test Tab 1 (Xem Preview HTML) và Bàn điều khiển Ký OTP
        // ══════════════════════════════════════════════════════════════
        var trade2 = new TradeTransaction
        {
            Id = Guid.Parse("41000000-0000-0000-0000-000000000002"),
            TradeTransactionCode = "TRD-20261010-002",
            TradeTransactionStatus = TradeTransactionStatus.PendingSignature,
            EscrowDeadline = DateTime.UtcNow.AddDays(1),
            SigningDeadline = DateTime.UtcNow.AddHours(24),
            TransferDeadline = DateTime.UtcNow.AddDays(4),
            CompletedAt = DateTime.UtcNow,
            AuctionId = auc6Id,
            BuyerId = bidderDaiGia,
            SellerId = sellerPhuQuy,
            SimNumberId = sim06
        };
        await context.TradeTransactions.AddAsync(trade2);

        var escrow2 = new Escrow
        {
            Id = Guid.Parse("42000000-0000-0000-0000-000000000002"),
            EscrowAmount = 120000000m,
            EscrowStatus = EscrowStatus.HELD,
            HeldAt = DateTime.UtcNow.AddMinutes(-10),
            AuctionDepositId = null,
            TradeTransactionId = trade2.Id
        };
        await context.Escrows.AddAsync(escrow2);

        var contract2 = new TransferContract
        {
            Id = Guid.Parse("43000000-0000-0000-0000-000000000002"),
            DocumentUrl = string.Empty,
            DocumentHash = string.Empty,
            BlockchainTxHash = string.Empty,
            ContractStatus = ContractStatus.DRAFT,
            TradeTransactionId = trade2.Id
        };
        await context.TransferContracts.AddAsync(contract2);

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  8. Level 6: Thông báo & Nhật ký kiểm toán hệ thống
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedNotificationsAndAuditLogsAsync(TrustSimDbContext context)
    {
        if (await context.Notifications.AnyAsync()) return;

        var adminId = (await context.UserAccounts.FirstOrDefaultAsync(u => u.UserName == "admin"))?.Id 
            ?? await context.UserAccounts.Select(u => u.Id).FirstOrDefaultAsync();
        if (adminId == Guid.Empty) return;

        var bidderKinhDoanh = (await context.UserAccounts.FirstOrDefaultAsync(u => u.UserName == "bidder_kinh_doanh"))?.Id 
            ?? adminId;
        var userSpamBot = (await context.UserAccounts.FirstOrDefaultAsync(u => u.UserName == "user_suspicious_bot"))?.Id 
            ?? adminId;

        var notifs = new List<Notification>
        {
            new()
            {
                Id = Guid.Parse("51000000-0000-0000-0000-000000000001"),
                Title = "Chúc mừng bạn đã trúng đấu giá!",
                Content = "Bạn đã trúng phiên đấu giá SIM 0909999999 với giá 210,000,000đ. Vui lòng ký hợp đồng chuyển nhượng.",
                ActionUrl = "/trade/TRD-20260830-001",
                IsRead = false,
                UserId = bidderKinhDoanh
            },
            new()
            {
                Id = Guid.Parse("51000000-0000-0000-0000-000000000002"),
                Title = "Cảnh báo bảo mật tài khoản",
                Content = "Tài khoản của bạn tạm thời bị khóa tham gia đấu giá do nghi vấn hành vi tự động.",
                ActionUrl = "/security/appeals",
                IsRead = false,
                UserId = userSpamBot
            }
        };
        await context.Notifications.AddRangeAsync(notifs);

        var auditLogs = new List<AuditLog>
        {
            new()
            {
                Id = Guid.Parse("52000000-0000-0000-0000-000000000001"),
                AuditLogName = "Phê duyệt hồ sơ eKYC",
                ActionType = ActionType.UPDATE,
                NewData = "{\"status\":\"VERIFIED\"}",
                OldData = "{\"status\":\"PENDING\"}",
                IPAddress = "118.70.124.55",
                ActorId = adminId,
                EntityId = Guid.Parse("12000000-0000-0000-0000-000000000003"),
                EntityName = "UserProfile"
            },
            new()
            {
                Id = Guid.Parse("52000000-0000-0000-0000-000000000002"),
                AuditLogName = "Khóa người dùng gian lận đấu giá",
                ActionType = ActionType.UPDATE,
                NewData = "{\"status\":\"BANNED\"}",
                OldData = "{\"status\":\"DEPOSIT_PAID\"}",
                IPAddress = "14.225.211.89",
                ActorId = adminId,
                EntityId = Guid.Parse("33000000-0000-0000-0000-000000000003"),
                EntityName = "AuctionParticipant"
            }
        };
        await context.AuditLogs.AddRangeAsync(auditLogs);

        await context.SaveChangesAsync();
    }
}
