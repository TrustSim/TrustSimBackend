using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence;

/// <summary>
/// Nạp dữ liệu ban đầu (Master Data, AI SIM Valuation RAG Knowledge & Test Scenarios) vào Database khi ứng dụng khởi động.
/// Tuân thủ quy chuẩn cây phụ thuộc (Level 0 -> Level 6) và đảm bảo Idempotent (chạy lại không lỗi).
/// Tất cả GUID sử dụng chuỗi Hexadecimal chuẩn [0-9a-f].
/// </summary>
public static class DbSeeder
{
    // BCrypt Hash của mật khẩu "Password123@" (WorkFactor: 11)
    private const string DefaultPasswordHash = "$2a$11$iZkkUucIpQldqKA4b4ArAupta/5ymSRsySEswzhGnRlBILSJkqyHC";

    public static async Task SeedAsync(TrustSimDbContext context)
    {
        // 1. Level 0: Bảng danh mục cơ bản & Documents
        await SeedMasterCatalogAsync(context);

        // 2. Level 1: Đầu số (PhonePrefixes)
        await SeedPhonePrefixesAsync(context);

        // 3. Level 0 & 1: Danh sách 10 Tài khoản, Hồ sơ, Cặp khóa PKI & Ví điện tử
        await SeedAccountsProfilesAndWalletsAsync(context);

        // 4. Level 2 & 3: Kho SIM số đẹp, Phân loại danh mục & Quyền sở hữu ban đầu
        await SeedSimsAndOwnershipsAsync(context);

        // 5. Level 3 & 4: Tri thức RAG nghiên cứu thị trường & Lịch sử Định giá AI
        await SeedAiValuationAndRagDocumentsAsync(context);

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

        // 4. Documents Pháp lý Cơ bản (d1000000-...)
        var legalDocs = new List<Document>
        {
            new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000001"), DocumentName = "Quy chế Đấu giá Trực tuyến TrustSim", LinkUrl = "https://trustsim.vn/legal/auction-regulation-2026.pdf", DocumentType = DocumentType.GENERAL, Status = DocumentStatus.INDEXED },
            new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000002"), DocumentName = "Điều khoản Dịch vụ Ký quỹ Escrow", LinkUrl = "https://trustsim.vn/legal/escrow-terms.pdf", DocumentType = DocumentType.GENERAL, Status = DocumentStatus.INDEXED },
            new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000003"), DocumentName = "Chính sách Xác thực eKYC & Ký số PKI", LinkUrl = "https://trustsim.vn/legal/ekyc-pki-policy.pdf", DocumentType = DocumentType.GENERAL, Status = DocumentStatus.INDEXED }
        };

        foreach (var doc in legalDocs)
        {
            if (!await context.Documents.AnyAsync(d => d.Id == doc.Id))
            {
                await context.Documents.AddAsync(doc);
            }
        }
        await context.SaveChangesAsync();
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
    //  3. Level 0 & 1: 10 Tài khoản, Hồ sơ, Cặp khóa PKI & Ví tiền
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAccountsProfilesAndWalletsAsync(TrustSimDbContext context)
    {
        if (await context.UserAccounts.AnyAsync(u => u.UserName == "seller_vip_phuquy")) return;

        var usersData = new[]
        {
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000001"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000001"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000001"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000001"), UserName = "admin",               Email = "admin@trustsim.vn",          Role = UserRole.ADMIN,      FullName = "Tổng Quản Trị Hệ Thống",          Phone = "0900000001", Identity = "001088000001", Dob = new DateTime(1988, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000002"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000002"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000002"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000002"), UserName = "consultant_01",       Email = "consultant01@trustsim.vn",  Role = UserRole.CONSULTANT, FullName = "Lê Hoàng Chuyên Viên",              Phone = "0900000002", Identity = "079092000002", Dob = new DateTime(1992, 5, 15, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000003"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000003"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000003"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000003"), UserName = "seller_vip_phuquy",   Email = "phuquy.sim@gmail.com",       Role = UserRole.USER,       FullName = "Trần Phú Quý (SIM Phú Quý)",       Phone = "0909999001", Identity = "001085009999", Dob = new DateTime(1985, 10, 20, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000004"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000004"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000004"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000004"), UserName = "seller_daiphat",      Email = "daiphat.telecom@gmail.com",  Role = UserRole.USER,       FullName = "Nguyễn Đại Phát (Đại Phát Telecom)",Phone = "0912333002", Identity = "079087008888", Dob = new DateTime(1987, 3, 12, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000005"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000005"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000005"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000005"), UserName = "bidder_dai_gia",      Email = "hoangnam.invest@gmail.com",  Role = UserRole.USER,       FullName = "Phạm Hoàng Nam (Đầu Tư BĐS)",      Phone = "0988777003", Identity = "001080007777", Dob = new DateTime(1980, 7, 8, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000006"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000006"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000006"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000006"), UserName = "bidder_kinh_doanh",  Email = "minhtri.bds@gmail.com",      Role = UserRole.USER,       FullName = "Vũ Minh Trí (Kinh Doanh)",         Phone = "0903444004", Identity = "079090006666", Dob = new DateTime(1990, 11, 25, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000007"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000007"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000007"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000007"), UserName = "bidder_phong_thuy",  Email = "quangduc.fengshui@gmail.com",Role = UserRole.USER,       FullName = "Đặng Quang Đức (Phong Thủy Gia)",  Phone = "0935555005", Identity = "048083005555", Dob = new DateTime(1983, 9, 9, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000008"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000008"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000008"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000008"), UserName = "user_ekyc_pending",  Email = "thanhha.new@gmail.com",      Role = UserRole.USER,       FullName = "Bùi Thanh Hà",                     Phone = "0977111006", Identity = "001098004444", Dob = new DateTime(1998, 12, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.PENDING_VERIFICATION, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000009"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000009"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000009"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000009"), UserName = "user_ekyc_rejected", Email = "vietcuong.fake@gmail.com",     Role = UserRole.USER,       FullName = "Ngô Việt Cường",                    Phone = "0944222007", Identity = "040095003333", Dob = new DateTime(1995, 4, 4, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.REJECTED, Available = 0m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000010"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000010"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000010"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000010"), UserName = "user_suspicious_bot", Email = "spambidder99@botmail.com",   Role = UserRole.USER,       FullName = "Bot Nghi Vấn Spam",                Phone = "0966000008", Identity = "001200002222", Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m, Locked = 0m, HasPki = false }
        };

        foreach (var u in usersData)
        {
            var existing = await context.UserAccounts.FirstOrDefaultAsync(x => x.Id == u.Id || x.UserName == u.UserName);
            if (existing != null) continue;

            var user = new UserAccount
            {
                Id = u.Id,
                UserName = u.UserName,
                UserEmail = u.Email,
                PassWordHash = DefaultPasswordHash,
                Role = u.Role,
                IsActive = true
            };

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

            user.UserProfile = profile;
            user.Wallet = wallet;

            if (u.HasPki)
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
                user.UserSecurityProfile = sec;
            }

            await context.UserAccounts.AddAsync(user);
        }

        await context.SaveChangesAsync();

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
    //  4. Level 2 & 3: Kho SIM, Phân loại & Quyền sở hữu
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedSimsAndOwnershipsAsync(TrustSimDbContext context)
    {
        if (await context.SimNumbers.AnyAsync()) return;

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

        var sims = new List<SimNumber>
        {
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000001"), PhoneNumber = "0988888888", Price = 1800000000m, SimStatus = SimStatus.IN_AUCTION, PhonePrefixId = pr098, FormatRhythmId = f28 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000002"), PhoneNumber = "0909999999", Price = 550000000m,  SimStatus = SimStatus.TRANSFERRED, PhonePrefixId = pr090, FormatRhythmId = f46 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000003"), PhoneNumber = "0912345678", Price = 105000000m,  SimStatus = SimStatus.IN_AUCTION, PhonePrefixId = pr091, FormatRhythmId = f28 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000004"), PhoneNumber = "0888686868", Price = 72000000m,   SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr088, FormatRhythmId = f22222 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000005"), PhoneNumber = "0797979799", Price = 35000000m,   SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr079, FormatRhythmId = f22222 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000006"), PhoneNumber = "0966668888", Price = 120000000m,  SimStatus = SimStatus.TRANSFERRED, PhonePrefixId = pr097, FormatRhythmId = f244 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000007"), PhoneNumber = "0986868686", Price = 2200000000m, SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr098, FormatRhythmId = f22222 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000008"), PhoneNumber = "0869192776", Price = 450000m,       SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr086, FormatRhythmId = f334 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000009"), PhoneNumber = "0903123678", Price = 2500000m,     SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr090, FormatRhythmId = f334 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000010"), PhoneNumber = "0977889900", Price = 8500000m,     SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr097, FormatRhythmId = f22222 },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000011"), PhoneNumber = "0339393939", Price = 18000000m,    SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr033, FormatRhythmId = f22222 },
        };
        await context.SimNumbers.AddRangeAsync(sims);

        var mappings = new List<SimCategoryMapping>
        {
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000001"), SimNumberId = sims[0].Id, SimCategoryId = caBatQuy,        ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000002"), SimNumberId = sims[0].Id, SimCategoryId = caKhongThatBat,   ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000003"), SimNumberId = sims[1].Id, SimCategoryId = caLucQuy,        ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000004"), SimNumberId = sims[1].Id, SimCategoryId = caKhongThatBat,   ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000005"), SimNumberId = sims[2].Id, SimCategoryId = caSanhTien,      ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000006"), SimNumberId = sims[3].Id, SimCategoryId = caLocPhatKep,    ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000007"), SimNumberId = sims[3].Id, SimCategoryId = caKhongThatBat,   ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000008"), SimNumberId = sims[4].Id, SimCategoryId = caThanTaiLon,    ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000009"), SimNumberId = sims[5].Id, SimCategoryId = caTuQuy,         ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000010"), SimNumberId = sims[5].Id, SimCategoryId = caKhongThatBat,   ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000011"), SimNumberId = sims[6].Id, SimCategoryId = caDaiTaxi86,     ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000012"), SimNumberId = sims[6].Id, SimCategoryId = caLocPhat86,     ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000013"), SimNumberId = sims[6].Id, SimCategoryId = caKhongThatBat,   ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000014"), SimNumberId = sims[7].Id, SimCategoryId = caGanhDao,       ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000015"), SimNumberId = sims[8].Id, SimCategoryId = caSanhTien,      ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000016"), SimNumberId = sims[9].Id, SimCategoryId = caLapDoi,        ScoreContribution = 1.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000017"), SimNumberId = sims[10].Id, SimCategoryId = caThanTai,       ScoreContribution = 1.0f },
        };
        await context.SimCategoryMappings.AddRangeAsync(mappings);

        var ownerships = new List<SimOwnership>
        {
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000001"), UserAccountId = sellerPhuQuy, SimNumberId = sims[0].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000002"), UserAccountId = sellerPhuQuy, SimNumberId = sims[1].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000003"), UserAccountId = sellerDaiPhat,SimNumberId = sims[2].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000004"), UserAccountId = sellerDaiPhat,SimNumberId = sims[3].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000005"), UserAccountId = sellerPhuQuy, SimNumberId = sims[6].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000006"), UserAccountId = sellerDaiPhat,SimNumberId = sims[7].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000007"), UserAccountId = sellerDaiPhat,SimNumberId = sims[8].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000008"), UserAccountId = sellerPhuQuy, SimNumberId = sims[9].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000009"), UserAccountId = sellerPhuQuy, SimNumberId = sims[10].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION }
        };
        await context.SimOwnerships.AddRangeAsync(ownerships);

        var verifications = new List<SimVerification>
        {
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000001"), SimOwnershipId = ownerships[0].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-29), VerifiedAt = DateTime.UtcNow.AddDays(-29) },
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000002"), SimOwnershipId = ownerships[1].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-29), VerifiedAt = DateTime.UtcNow.AddDays(-29) },
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000003"), SimOwnershipId = ownerships[2].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-19), VerifiedAt = DateTime.UtcNow.AddDays(-19) },
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000004"), SimOwnershipId = ownerships[4].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-10), VerifiedAt = DateTime.UtcNow.AddDays(-10) }
        };
        await context.SimVerifications.AddRangeAsync(verifications);

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  5. Level 3 & 4: Tri thức RAG & Lịch sử Định giá AI
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAiValuationAndRagDocumentsAsync(TrustSimDbContext context)
    {
        // 1. Documents Kho tri thức RAG (d1000000-...)
        var doc1Id = Guid.Parse("d1000000-0000-0000-0000-000000000004");
        var doc2Id = Guid.Parse("d1000000-0000-0000-0000-000000000005");
        var doc3Id = Guid.Parse("d1000000-0000-0000-0000-000000000006");

        var ragDocs = new List<Document>
        {
            new()
            {
                Id = doc1Id,
                DocumentName = "Cẩm nang Định giá SIM Số Đẹp & Xu hướng Thị trường Viễn thông 2026",
                LinkUrl = "https://trustsim.vn/knowledge/market-valuation-guide-2026.md",
                DocumentType = DocumentType.MARKET_RESEARCH,
                Status = DocumentStatus.INDEXED,
                TotalChunks = 4,
                TotalTokens = 1200,
                CrawledAt = DateTime.UtcNow.AddDays(-1),
                RawContent = @"# CẨM NANG ĐỊNH GIÁ SIM SỐ ĐẸP & XU HƯỚNG THỊ TRƯỜNG VIỄN THÔNG 2026

## 1. Phân cấp Đầu số và Uy tín Thương hiệu
Đầu số viễn thông quyết định 30% đến 45% giá trị của một số SIM.
- Đầu số cổ Tam Đại Nhà Mạng (Viettel 098/097/096, MobiFone 090/093, VinaPhone 091/094) có hệ số uy tín cao nhất (1.4 - 1.5). Đây là các đầu số biểu tượng của doanh nhân và giới thượng lưu.
- Đầu số Song Phát / Đại Phát như 088 (Vina Song Phát Đại Phát), 086 (Viettel Phát Lộc), 089 (Mobi Phát Tài) giữ hệ số 1.25 - 1.30.
- Các đầu số chuyển đổi từ 11 số (03x, 07x, 08x) có tính thanh khoản cao, phù hợp phân khúc tầm trung với hệ số 1.10 - 1.15.

## 2. Ma trận Định giá Dòng SIM Siêu VIP
- Bát quý (8 số trùng lặp): Mức giá dao động từ 1.0 tỷ đến trên 5.0 tỷ VND. Bát quý 88888888 hoặc 99999999 thuộc nhóm di sản vô giá.
- Lục quý (6 số trùng lặp cuối): Dao động từ 300 triệu đến 1.5 tỷ VND tùy thuộc vào chữ số (Lục quý 8, 9, 6 cao hơn lục quý 0, 4, 7).
- Ngũ quý (5 số trùng lặp cuối): Dao động từ 80 triệu đến 350 triệu VND.
- Tứ quý (4 số trùng lặp cuối): Dao động từ 15 triệu đến 150 triệu VND. Tứ quý kết hợp đầu số cổ luôn đạt mức giá trần.

## 3. Định giá SIM Doanh nhân & Sảnh tiến
- Sảnh tiến Rồng 6 số (VD: 456789, 123456): Biểu tượng của sự thăng tiến không ngừng, định giá từ 50 triệu đến 250 triệu VND.
- Sảnh 6789 ('San bằng tất cả'): Dòng số kinh điển của giới kinh doanh, giá trị từ 15 triệu đến 80 triệu VND.
- Taxi 3 Cặp (ABABAB hoặc ABCABC): Dễ nhớ tuyệt đối cho hotline doanh nghiệp, giá trị từ 20 triệu đến 90 triệu VND.

## 4. Thanh khoản & Biên độ Giá Thị trường
Giá trị giao dịch thực tế phụ thuộc vào chu kỳ thanh khoản:
- Mức giá tối thiểu (MinPrice): Giá thanh lý nhanh trong vòng 24h (chiết khấu 15% - 20% so với giá đề xuất).
- Mức giá đề xuất (SuggestedPrice): Mức giá giao dịch cân bằng tối ưu trên sàn đấu giá TrustSim.
- Mức giá tối đa (MaxPrice): Mức giá kỳ vọng cao khi gặp đúng khách hàng tìm kiếm số trùng năm sinh, biển số hoặc hợp phong thủy đặc thù."
            },
            new()
            {
                Id = doc2Id,
                DocumentName = "Quy chuẩn Phong thủy SIM: Ngũ hành, Cát tinh & Ý nghĩa Dãy số",
                LinkUrl = "https://trustsim.vn/knowledge/fengshui-rules-and-meanings.md",
                DocumentType = DocumentType.FENG_SHUI_RULE,
                Status = DocumentStatus.INDEXED,
                TotalChunks = 3,
                TotalTokens = 950,
                CrawledAt = DateTime.UtcNow.AddDays(-1),
                RawContent = @"# QUY CHUẨN PHONG THỦY SIM: NGŨ HÀNH, CÁT TINH & Ý NGHĨA DÃY SỐ

## 1. Ý nghĩa Số học và Các Cặp số Cát Tường
- Cặp 68 / 86 (Lộc Phát - Phát Lộc): Thu hút tài lộc dồi dào, vượng khí kinh doanh. Đuôi 6868 là thế 'Đại Lộc Kép'.
- Cặp 39 / 79 (Thần Tài Nhỏ / Thần Tài Lớn): Đem lại may mắn, quý nhân phù trợ trong thương trường. Dãy số 3979 kết hợp cả Thần tài lớn nhỏ.
- Cặp 38 / 78 (Ông Địa Nhỏ / Ông Địa Lớn): Rất thịnh hành trong giới đầu tư bất động sản, xây dựng và tài chính.
- Cụm 1368 (Sinh Tài Lộc Phát): Chuỗi số phong thủy đỉnh cao mang hàm nghĩa tài lộc sinh sôi bền vững bốn mùa (1+3+6+8 = 18 = Nhất Phát).

## 2. Ngũ hành Bản Mệnh & Quy tắc Tương sinh
Mỗi con số đại diện cho một hành trong Ngũ hành:
- Hành Kim: 6, 7 (Hợp người mệnh Thủy và mệnh Kim, khắc mệnh Mộc).
- Hành Mộc: 3, 4 (Hợp người mệnh Hỏa và mệnh Mộc, khắc mệnh Thổ).
- Hành Thủy: 0, 1 (Hợp người mệnh Mộc và mệnh Thủy, khắc mệnh Hỏa).
- Hành Hỏa: 9 (Hợp người mệnh Thổ và mệnh Hỏa, khắc mệnh Kim).
- Hành Thổ: 2, 5, 8 (Hợp người mệnh Kim và mệnh Thổ, khắc mệnh Thủy).
Một số SIM có ngũ hành tương sinh với bản mệnh gia chủ sẽ giúp kích hoạt tài vận và bình an.

## 3. Các Quy tắc Kiêng kỵ Dân gian
- Đuôi số 49, 53: Theo quan niệm dân gian là 'hạn 49 chưa qua 53 đã tới', làm giảm 20% - 30% giá trị thương mại của SIM trừ khi là SIM giá rẻ.
- SIM Không 4 - 7 (Sạch số cát khí): Dòng SIM không chứa bất kỳ chữ số 4 (Tử) và 7 (Thất) nào luôn được định giá cao hơn 30% - 40% so với SIM cùng loại vì tính thanh khiết và may mắn toàn diện."
            },
            new()
            {
                Id = doc3Id,
                DocumentName = "Hướng dẫn Phân tích & Định giá SIM Tự động Đa tầng TrustSim",
                LinkUrl = "https://trustsim.vn/knowledge/trustsim-valuation-methodology.md",
                DocumentType = DocumentType.PRICING_GUIDELINE,
                Status = DocumentStatus.INDEXED,
                TotalChunks = 3,
                TotalTokens = 850,
                CrawledAt = DateTime.UtcNow.AddDays(-1),
                RawContent = @"# HƯỚNG DẪN PHÂN TÍCH & ĐỊNH GIÁ SIM TỰ ĐỘNG ĐA TẦNG TRUSTSIM

## 1. Kiến trúc Định giá 4 Tầng (Hybrid 4-Tier Valuation)
- Tầng 1 (Feature Extraction): Bóc tách số điện thoại thành các đặc trưng cấu trúc: đầu số, nhịp số (Regex), danh mục phong thủy và yếu tố kiêng kỵ.
- Tầng 2 (Mathematical Engine): Tính toán mức giá cơ sở P_base dựa trên công thức toán học chuẩn:
  `P_base = P0 * Factor_Prefix * Factor_Rhythm * Factor_Category * Factor_FengShui`
  Trong đó P0 = 200,000 VND (Mức giá SIM trắng tiêu chuẩn).
- Tầng 3 (RAG Market Context): Truy vấn tri thức thị trường thời gian thực từ NeonDB pgvector để điều chỉnh hệ số thị trường (Market Multiplier từ 0.9 đến 1.35).
- Tầng 4 (LLM Reasoning & Validation): NVIDIA LLM (Nemotron / DeepSeek) tổng hợp toàn bộ dữ liệu, thẩm định khoảng giá Min-Max và sinh bài giải trình chuyên sâu cho khách hàng.

## 2. Quy chuẩn Làm tròn Thương mại (Commercial Rounding)
Để đảm bảo mức giá hiển thị tự nhiên và dễ giao dịch:
- Dưới 1 triệu VND: Làm tròn đến 10,000 VND.
- Từ 1 triệu đến 10 triệu VND: Làm tròn đến 50,000 VND.
- Từ 10 triệu đến 100 triệu VND: Làm tròn đến 500,000 VND.
- Trên 100 triệu VND: Làm tròn đến 1,000,000 VND.

## 3. Độ Tin cậy (Confidence Score)
- Điểm từ 0.90 - 1.00: Áp dụng cho các mẫu số phổ biến, có đầy đủ căn cứ nhịp số, đầu số chuẩn và dữ liệu RAG tương đồng cao.
- Điểm từ 0.70 - 0.89: Áp dụng cho các mẫu số hỗn hợp hoặc đầu số nhà mạng mới."
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
                existing.RawContent = doc.RawContent;
                existing.Status = doc.Status;
                existing.DocumentType = doc.DocumentType;
            }
        }
        await context.SaveChangesAsync();

        // 2. DocumentChunks (dc000000-...)
        if (!await context.DocumentChunks.AnyAsync())
        {
            var chunks = new List<DocumentChunk>
            {
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000001"),
                    DocumentId = doc1Id,
                    ChunkIndex = 0,
                    TokenCount = 280,
                    MetadataJson = "{\"title\":\"Phân cấp Đầu số và Uy tín Nhà mạng\",\"keywords\":[\"098\",\"090\",\"091\",\"088\",\"dau_so_co\"]}",
                    Content = "Đầu số viễn thông quyết định 30% đến 45% giá trị của một số SIM. Đầu số cổ Tam Đại Nhà Mạng (Viettel 098/097/096, MobiFone 090/093, VinaPhone 091/094) có hệ số uy tín cao nhất (1.4 - 1.5). Đây là các đầu số biểu tượng của doanh nhân và giới thượng lưu. Đầu số Song Phát / Đại Phát như 088 (Vina Song Phát Đại Phát), 086 (Viettel Phát Lộc), 089 (Mobi Phát Tài) giữ hệ số 1.25 - 1.30. Các đầu số chuyển đổi từ 11 số (03x, 07x, 08x) có tính thanh khoản cao, phù hợp phân khúc tầm trung với hệ số 1.10 - 1.15."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000002"),
                    DocumentId = doc1Id,
                    ChunkIndex = 1,
                    TokenCount = 380,
                    MetadataJson = "{\"title\":\"Ma trận Định giá Dòng SIM Siêu VIP\",\"keywords\":[\"bat_quy\",\"luc_quy\",\"ngu_quy\",\"tu_quy\",\"simthanglong\",\"chosim24h\",\"bang_gia\"]}",
                    Content = "Ma trận định giá thị trường thực tế (Tham chiếu sàn Sim Thăng Long simthanglong.vn, Chợ Sim 24h): Bát quý (8 số trùng lặp): Mức giá dao động từ 15 tỷ đến trên 35 tỷ VND. Lục quý (6 số trùng lặp cuối): Dao động từ 4.0 tỷ đến 15 tỷ VND tùy thuộc vào chữ số và đầu mạng (Lục quý 8, 9, 6 đầu cổ 098/090/091 đạt đỉnh giá). Ngũ quý (5 số trùng lặp cuối): Phân cấp rõ rệt: Ngũ quý 6, 8, 9 kết hợp đầu cổ 098/090/091 (như 098.56.88888, 098.x.88888) có giá niêm yết showroom/đại lý từ 1.8 tỷ đến 2.3 tỷ VND (giá tham chiếu thực tế tại simthanglong.vn: ~1.888.000.000 đ), giá giao dịch P2P từ 1.3 tỷ đến 1.7 tỷ VND, giá thợ thu mua nhanh 900 triệu đến 1.2 tỷ VND; Ngũ quý 2, 3, 5, 7 có giá từ 300 triệu đến 700 triệu VND; Ngũ quý 0, 1, 4 đầu mới dao động từ 100 triệu đến 250 triệu VND. Tứ quý (4 số đuôi): Tứ quý 6666, 8888, 9999 đầu cổ 098/090/091 có giá từ 400 triệu đến 900 triệu VND, đầu mới từ 150 triệu đến 400 triệu VND; Tứ quý 1, 2, 3, 5, 7 từ 50 triệu đến 200 triệu VND."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000003"),
                    DocumentId = doc1Id,
                    ChunkIndex = 2,
                    TokenCount = 290,
                    MetadataJson = "{\"title\":\"Định giá SIM Doanh nhân & Sảnh tiến\",\"keywords\":[\"sanh_tien\",\"6789\",\"taxi\",\"tam_hoa_kep\"]}",
                    Content = "Sảnh tiến Rồng 6 số (VD: 456789, 123456): Biểu tượng của sự thăng tiến không ngừng, định giá từ 800 triệu đến 2.5 tỷ VND với đầu cổ 098/090/091. Sảnh tiến 6789 ('San bằng tất cả'): Dòng số kinh điển của giới kinh doanh, giá trị từ 150 triệu đến 400 triệu VND đầu cổ, đầu mới từ 40 triệu đến 120 triệu VND. Đại Taxi 4 Cặp (86.86.86.86, 68.68.68.68): Định giá từ 1.5 tỷ đến 6 tỷ VND. Taxi 3 Cặp (ABABAB hoặc ABCABC): Dễ nhớ tuyệt đối cho hotline doanh nghiệp, giá trị từ 50 triệu đến 250 triệu VND. Tam hoa kép (AAABBB): Định giá từ 30 triệu đến 120 triệu VND."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000004"),
                    DocumentId = doc1Id,
                    ChunkIndex = 3,
                    TokenCount = 260,
                    MetadataJson = "{\"title\":\"Thanh khoản & 3 Kênh Giao dịch Thị trường\",\"keywords\":[\"min_price\",\"max_price\",\"suggested_price\",\"thanh_khoan\",\"simthanglong\"]}",
                    Content = "Biên độ giá 3 kênh giao dịch thực tế: Kênh 1 - Giá Niêm Yết Showroom/Đại Lý Lớn (MaxPrice): Giá bán lẻ chính thức tại các hệ thống như Sim Thăng Long, Chợ Sim 24h, hỗ trợ bảo hành trọn đời và trả góp. Kênh 2 - Giá Giao Dịch Chợ Tự Do P2P (SuggestedPrice): Giá chuyển nhượng trực tiếp giữa các người dùng hoặc bán thẳng cho doanh nhân, phản ánh giá trị thực tế hợp lý nhất. Kênh 3 - Giá Thợ Thu Mua Nhanh 24h (MinPrice): Giá thợ hoặc thương lái sẵn sàng chi tiền mặt thu mua trong 24h (chiết khấu an toàn 25% - 35% so với giá P2P)."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000005"),
                    DocumentId = doc2Id,
                    ChunkIndex = 0,
                    TokenCount = 300,
                    MetadataJson = "{\"title\":\"Ý nghĩa Số học và Cặp số Cát Tường\",\"keywords\":[\"loc_phat\",\"than_tai\",\"ong_dia\",\"1368\"]}",
                    Content = "Cặp 68 / 86 (Lộc Phát - Phát Lộc): Thu hút tài lộc dồi dào, vượng khí kinh doanh. Đuôi 6868 là thế 'Đại Lộc Kép'. Cặp 39 / 79 (Thần Tài Nhỏ / Thần Tài Lớn): Đem lại may mắn, quý nhân phù trợ trong thương trường. Dãy số 3979 kết hợp cả Thần tài lớn nhỏ. Cặp 38 / 78 (Ông Địa Nhỏ / Ông Địa Lớn): Rất thịnh hành trong giới đầu tư bất động sản, xây dựng và tài chính. Cụm 1368 (Sinh Tài Lộc Phát): Chuỗi số phong thủy đỉnh cao mang hàm nghĩa tài lộc sinh sôi bền vững bốn mùa (1+3+6+8 = 18 = Nhất Phát)."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000006"),
                    DocumentId = doc2Id,
                    ChunkIndex = 1,
                    TokenCount = 290,
                    MetadataJson = "{\"title\":\"Ngũ hành Bản Mệnh & Quy tắc Tương sinh\",\"keywords\":[\"ngu_hanh\",\"ban_menh\",\"kim_moc_thuy_hoa_tho\"]}",
                    Content = "Mỗi con số đại diện cho một hành trong Ngũ hành: Hành Kim: 6, 7 (Hợp người mệnh Thủy và mệnh Kim, khắc mệnh Mộc). Hành Mộc: 3, 4 (Hợp người mệnh Hỏa và mệnh Mộc, khắc mệnh Thổ). Hành Thủy: 0, 1 (Hợp người mệnh Mộc và mệnh Thủy, khắc mệnh Hỏa). Hành Hỏa: 9 (Hợp người mệnh Thổ và mệnh Hỏa, khắc mệnh Kim). Hành Thổ: 2, 5, 8 (Hợp người mệnh Kim và mệnh Thổ, khắc mệnh Thủy). Một số SIM có ngũ hành tương sinh với bản mệnh gia chủ sẽ giúp kích hoạt tài vận và bình an."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000007"),
                    DocumentId = doc2Id,
                    ChunkIndex = 2,
                    TokenCount = 220,
                    MetadataJson = "{\"title\":\"Các Quy tắc Kiêng kỵ Dân gian\",\"keywords\":[\"4953\",\"so_sach\",\"khong_that_bat\"]}",
                    Content = "Đuôi số 49, 53: Theo quan niệm dân gian là 'hạn 49 chưa qua 53 đã tới', làm giảm 20% - 30% giá trị thương mại của SIM trừ khi là SIM giá rẻ. SIM Không 4 - 7 (Sạch số cát khí): Dòng SIM không chứa bất kỳ chữ số 4 (Tử) và 7 (Thất) nào luôn được định giá cao hơn 30% - 40% so với SIM cùng loại vì tính thanh khiết và may mắn toàn diện."
                },
                new()
                {
                    Id = Guid.Parse("dc000000-0000-0000-0000-000000000008"),
                    DocumentId = doc3Id,
                    ChunkIndex = 0,
                    TokenCount = 310,
                    MetadataJson = "{\"title\":\"Kiến trúc Định giá 4 Tầng TrustSim\",\"keywords\":[\"hybrid_valuation\",\"rag\",\"llm\",\"cong_thuc_gia\"]}",
                    Content = "Kiến trúc Định giá 4 Tầng TrustSim: Tầng 1 (Feature Extraction): Bóc tách số điện thoại thành các đặc trưng cấu trúc: đầu số, nhịp số (Regex), danh mục phong thủy và yếu tố kiêng kỵ. Tầng 2 (Mathematical Engine): Tính toán mức giá cơ sở P_base dựa trên công thức toán học chuẩn: P_base = P0 * Factor_Prefix * Factor_Rhythm * Factor_Category * Factor_FengShui trong đó P0 = 200,000 VND. Tầng 3 (RAG Market Context): Truy vấn tri thức thị trường thời gian thực từ NeonDB pgvector để điều chỉnh hệ số thị trường (Market Multiplier). Tầng 4 (LLM Reasoning): NVIDIA LLM tổng hợp toàn bộ dữ liệu, thẩm định khoảng giá Min-Max và sinh bài giải trình chuyên sâu."
                }
            };
            await context.DocumentChunks.AddRangeAsync(chunks);
            await context.SaveChangesAsync();
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
        if (await context.Auctions.AnyAsync()) return;

        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var sellerDaiPhat = Guid.Parse("11000000-0000-0000-0000-000000000004");
        var bidderDaiGia = Guid.Parse("11000000-0000-0000-0000-000000000005");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");
        var bidderPhongThuy = Guid.Parse("11000000-0000-0000-0000-000000000007");
        var userSpamBot = Guid.Parse("11000000-0000-0000-0000-000000000010");

        var sim01 = Guid.Parse("21000000-0000-0000-0000-000000000001"); // 0988888888
        var sim02 = Guid.Parse("21000000-0000-0000-0000-000000000002"); // 0909999999
        var sim03 = Guid.Parse("21000000-0000-0000-0000-000000000003"); // 0912345678

        // 1. Auctions (31000000-...)
        var auc1 = new Auction
        {
            Id = Guid.Parse("31000000-0000-0000-0000-000000000001"),
            AuctionCode = "AUC-20260901-001",
            AuctionType = AuctionType.ENGLISH,
            AuctionStatus = AuctionStatus.ACTIVE,
            StartingPrice = 1000000000m,
            CurrentPrice = 1200000000m,
            ReservePrice = 1100000000m,
            StartAt = DateTime.UtcNow.AddDays(-1),
            EndAt = DateTime.UtcNow.AddDays(2),
            RegistrationStartAt = DateTime.UtcNow.AddDays(-5),
            RegistrationEndAt = DateTime.UtcNow.AddDays(-1),
            LockVersion = 3,
            AuthorId = sellerPhuQuy,
            SimNumberId = sim01
        };

        var auc2 = new Auction
        {
            Id = Guid.Parse("31000000-0000-0000-0000-000000000002"),
            AuctionCode = "AUC-20260905-002",
            AuctionType = AuctionType.DUTCH,
            AuctionStatus = AuctionStatus.ACTIVE,
            StartingPrice = 120000000m,
            CurrentPrice = 114000000m,
            ReservePrice = 80000000m,
            StartAt = DateTime.UtcNow.AddHours(-3),
            EndAt = DateTime.UtcNow.AddDays(1),
            RegistrationStartAt = DateTime.UtcNow.AddDays(-2),
            RegistrationEndAt = DateTime.UtcNow.AddHours(-3),
            LockVersion = 1,
            AuthorId = sellerDaiPhat,
            SimNumberId = sim03
        };

        var auc3 = new Auction
        {
            Id = Guid.Parse("31000000-0000-0000-0000-000000000003"),
            AuctionCode = "AUC-20260828-003",
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

        await context.Auctions.AddRangeAsync(auc1, auc2, auc3);

        // 2. Auction Rules (32000000-...)
        var rules = new List<AuctionRule>
        {
            new() { Id = Guid.Parse("32000000-0000-0000-0000-000000000001"), AuctionId = auc1.Id, MinimumBidIncrement = 50000000m, DepositAmount = 100000000m, MaximumBidCountPerParticipant = 50, MaximumBidCountPerMinute = 10, MinimumBidIntervalSeconds = 5.0f, ExtensionThresholdSeconds = 120.0f, ExtensionDurationSeconds = 300.0f, MaximumExtensionCount = 5 },
            new() { Id = Guid.Parse("32000000-0000-0000-0000-000000000002"), AuctionId = auc2.Id, MinimumBidIncrement = 0m,        DepositAmount = 10000000m,  MaximumBidCountPerParticipant = 1,  MaximumBidCountPerMinute = 1,  MinimumBidIntervalSeconds = 0.0f, DutchDecreaseAmount = 2000000m, DutchDecreaseIntervalSeconds = 1800.0f },
            new() { Id = Guid.Parse("32000000-0000-0000-0000-000000000003"), AuctionId = auc3.Id, MinimumBidIncrement = 10000000m, DepositAmount = 20000000m,  MaximumBidCountPerParticipant = 50, MaximumBidCountPerMinute = 10, MinimumBidIntervalSeconds = 5.0f, ExtensionThresholdSeconds = 120.0f, ExtensionDurationSeconds = 300.0f, MaximumExtensionCount = 5 }
        };
        await context.AuctionRules.AddRangeAsync(rules);

        // 3. Auction Participants (33000000-...)
        var ap1 = new AuctionParticipant { Id = Guid.Parse("33000000-0000-0000-0000-000000000001"), AuctionId = auc1.Id, UserAccountId = bidderDaiGia, Status = ParticipantStatus.DEPOSIT_PAID, AcceptedBidCount = 1, JoinedAt = DateTime.UtcNow.AddDays(-2) };
        var ap2 = new AuctionParticipant { Id = Guid.Parse("33000000-0000-0000-0000-000000000002"), AuctionId = auc1.Id, UserAccountId = bidderKinhDoanh, Status = ParticipantStatus.DEPOSIT_PAID, AcceptedBidCount = 1, JoinedAt = DateTime.UtcNow.AddDays(-2) };
        var ap3 = new AuctionParticipant { Id = Guid.Parse("33000000-0000-0000-0000-000000000003"), AuctionId = auc1.Id, UserAccountId = userSpamBot, Status = ParticipantStatus.BANNED, RejectedBidCount = 5, JoinedAt = DateTime.UtcNow.AddDays(-2), BlockedAt = DateTime.UtcNow.AddHours(-12), BlockedReason = "Phát hiện spam 20 bids trong 5s" };
        var ap4 = new AuctionParticipant { Id = Guid.Parse("33000000-0000-0000-0000-000000000004"), AuctionId = auc2.Id, UserAccountId = bidderPhongThuy, Status = ParticipantStatus.DEPOSIT_PAID, JoinedAt = DateTime.UtcNow.AddDays(-1) };
        var ap5 = new AuctionParticipant { Id = Guid.Parse("33000000-0000-0000-0000-000000000005"), AuctionId = auc3.Id, UserAccountId = bidderKinhDoanh, Status = ParticipantStatus.DEPOSIT_PAID, AcceptedBidCount = 3, JoinedAt = DateTime.UtcNow.AddDays(-6) };

        await context.AuctionParticipants.AddRangeAsync(ap1, ap2, ap3, ap4, ap5);

        // 4. Bids (34000000-...)
        var bids = new List<Bid>
        {
            new() { Id = Guid.Parse("34000000-0000-0000-0000-000000000001"), AuctionId = auc1.Id, ParticipantId = ap2.Id, BidAmount = 1050000000m, Status = BidStatus.OUTBIB,  PlacedAt = DateTime.UtcNow.AddHours(-18) },
            new() { Id = Guid.Parse("34000000-0000-0000-0000-000000000002"), AuctionId = auc1.Id, ParticipantId = ap3.Id, BidAmount = 1100000000m, Status = BidStatus.REJECTED, PlacedAt = DateTime.UtcNow.AddHours(-12), RejectedAt = DateTime.UtcNow.AddHours(-12), RejectedReason = "Rate limit exceeded" },
            new() { Id = Guid.Parse("34000000-0000-0000-0000-000000000003"), AuctionId = auc1.Id, ParticipantId = ap1.Id, BidAmount = 1200000000m, Status = BidStatus.WINNING, PlacedAt = DateTime.UtcNow.AddHours(-6) },
            new() { Id = Guid.Parse("34000000-0000-0000-0000-000000000004"), AuctionId = auc3.Id, ParticipantId = ap5.Id, BidAmount = 210000000m,  Status = BidStatus.WINNING, PlacedAt = DateTime.UtcNow.AddDays(-2) }
        };
        await context.Bids.AddRangeAsync(bids);

        // 5. Fraud Assessment (35000000-...)
        var fa = new FraudAssessment
        {
            Id = Guid.Parse("35000000-0000-0000-0000-000000000001"),
            ParticipantId = ap3.Id,
            RiskScore = 95.5f,
            RiskLevel = RiskLevel.CRITICAL,
            FraudSignal = "Phát hiện 20 lượt bid trong 5s từ cùng 1 IP proxy nghi vấn botnet",
            AssessmentDecision = AssessmentDecision.BLOCK_PARTICIPANT,
            AssessmentStatus = AssessmentStatus.AUTOMATED,
            AssessedAt = DateTime.UtcNow.AddHours(-12)
        };
        await context.FraudAssessments.AddAsync(fa);

        // 6. Auction Result for Session 3 (36000000-...)
        var res = new AuctionResult
        {
            Id = Guid.Parse("36000000-0000-0000-0000-000000000001"),
            AuctionId = auc3.Id,
            WinnerId = ap5.Id,
            FinalPrice = 210000000m,
            AuctionResultStatus = AuctionResultStatus.CONFIRMED,
            DeterminedAt = DateTime.UtcNow.AddDays(-2),
            ConfirmedAt = DateTime.UtcNow.AddDays(-2)
        };
        await context.AuctionResults.AddAsync(res);
        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  7. Level 4 -> 6: Giao dịch chuyển nhượng, Hợp đồng & Ký quỹ
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedTradeAndContractsAsync(TrustSimDbContext context)
    {
        if (await context.TradeTransactions.AnyAsync()) return;

        var auc3Id = Guid.Parse("31000000-0000-0000-0000-000000000003");
        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");
        var sim02 = Guid.Parse("21000000-0000-0000-0000-000000000002");

        // 1. TradeTransaction (41000000-...)
        var trade = new TradeTransaction
        {
            Id = Guid.Parse("41000000-0000-0000-0000-000000000001"),
            TradeTransactionCode = "TRD-20260830-001",
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
            EscrowDeadline = DateTime.UtcNow.AddDays(1),
            SigningDeadline = DateTime.UtcNow.AddDays(2),
            TransferDeadline = DateTime.UtcNow.AddDays(5),
            CompletedAt = DateTime.UtcNow,
            AuctionId = auc3Id,
            BuyerId = bidderKinhDoanh,
            SellerId = sellerPhuQuy,
            SimNumberId = sim02
        };
        await context.TradeTransactions.AddAsync(trade);

        // 2. Escrow (42000000-...)
        var escrow = new Escrow
        {
            Id = Guid.Parse("42000000-0000-0000-0000-000000000001"),
            EscrowAmount = 0m,
            EscrowStatus = EscrowStatus.HELD,
            HeldAt = DateTime.UtcNow.AddDays(-2),
            AuctionDepositId = null,
            TradeTransactionId = trade.Id
        };
        await context.Escrows.AddAsync(escrow);

        // 3. TransferContract (43000000-...)
        var contract = new TransferContract
        {
            Id = Guid.Parse("43000000-0000-0000-0000-000000000001"),
            DocumentUrl = "https://storage.trustsim.vn/contracts/TRD-20260830-001.pdf",
            DocumentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            BlockchainTxHash = "0x7f9fad1c0e5a83b275c4e976ec4e0626a23531b79f83526d11f71a938c0f5f84",
            ContractStatus = ContractStatus.FULLY_SIGNED,
            TradeTransactionId = trade.Id
        };
        await context.TransferContracts.AddAsync(contract);

        // 4. DigitalSignatures (44000000-...)
        var sigSeller = new DigitalSignature
        {
            Id = Guid.Parse("44000000-0000-0000-0000-000000000001"),
            SignatureRole = SignatureRole.SELLER,
            PublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA3003...",
            CryptographicSignature = "KycSig_Seller_PhuQuy_RSA_Signature_Hash_Value_001...",
            SignedAt = DateTime.UtcNow.AddDays(-1),
            ContracId = contract.Id,
            UserId = sellerPhuQuy
        };

        var sigBuyer = new DigitalSignature
        {
            Id = Guid.Parse("44000000-0000-0000-0000-000000000002"),
            SignatureRole = SignatureRole.BUYER,
            PublicKey = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEA6006...",
            CryptographicSignature = "KycSig_Buyer_MinhTri_RSA_Signature_Hash_Value_002...",
            SignedAt = DateTime.UtcNow.AddHours(-22),
            ContracId = contract.Id,
            UserId = bidderKinhDoanh
        };
        await context.DigitalSignatures.AddRangeAsync(sigSeller, sigBuyer);

        // 5. SimTransfer & Proof (45000000-... & 46000000-...)
        var transfer = new SimTransfer
        {
            Id = Guid.Parse("45000000-0000-0000-0000-000000000001"),
            StartAt = DateTime.UtcNow.AddDays(-1),
            Deadline = DateTime.UtcNow.AddDays(3),
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED,
            TradeTransactionId = trade.Id
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
