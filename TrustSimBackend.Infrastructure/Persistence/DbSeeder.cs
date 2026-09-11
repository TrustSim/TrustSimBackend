using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence;

/// <summary>
/// Nạp dữ liệu ban đầu (Master Data & Test Scenarios) vào Database khi ứng dụng khởi động.
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

        // 5. Level 4 & 5: Các phiên đấu giá (English, Dutch, End), Quy tắc, Người tham gia, Bids & Gian lận
        await SeedAuctionsAndBidsAsync(context);

        // 6. Level 4 -> 6: Giao dịch chuyển nhượng, Hợp đồng số, Ký quỹ Escrow, Chữ ký số & Bằng chứng
        await SeedTradeAndContractsAsync(context);

        // 7. Level 6: Thông báo người dùng & Nhật ký kiểm toán hệ thống
        await SeedNotificationsAndAuditLogsAsync(context);

        Console.WriteLine("[DbSeeder] ✅ Toàn bộ Seed Data đa kịch bản đã được nạp thành công.");
    }

    // ─────────────────────────────────────────────────────────────
    //  1. Level 0: Master Catalog (Nhà mạng, Nhịp số, Danh mục, Docs)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedMasterCatalogAsync(TrustSimDbContext context)
    {
        // FormatRhythms (f1000000-...)
        if (!await context.FormatRhythms.AnyAsync())
        {
            var rhythms = new List<FormatRhythm>
            {
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000001"), FormatRhythmName = "Bát quý",    Value = "AAAAAAAA", Description = "8 chữ số giống nhau liên tiếp. VD: 0988 888888" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000002"), FormatRhythmName = "Lục quý",    Value = "AAAAAA",   Description = "6 chữ số giống nhau liên tiếp. VD: 090 999999" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000003"), FormatRhythmName = "Ngũ quý",    Value = "AAAAA",    Description = "5 chữ số giống nhau liên tiếp. VD: 0988 88888" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000004"), FormatRhythmName = "Tứ quý",     Value = "AAAA",     Description = "4 chữ số giống nhau cuối số. VD: 0909 9999" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000005"), FormatRhythmName = "Sảnh tiến",  Value = "ABCDE",    Description = "Chuỗi chữ số tăng dần liên tiếp. VD: 091 2345678" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000006"), FormatRhythmName = "Sảnh lùi",   Value = "EDCBA",    Description = "Chuỗi chữ số giảm dần liên tiếp. VD: 090 9876543" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000007"), FormatRhythmName = "Lặp đôi",    Value = "ABABAB",   Description = "Hai chữ số xen kẽ luân phiên. VD: 0909 090909" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000008"), FormatRhythmName = "Gánh đảo",   Value = "ABBA",     Description = "4 số đối xứng. VD: 0909 1221" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000009"), FormatRhythmName = "Lộc phát",   Value = "68",       Description = "Chứa cặp số 68 hoặc 86 liên quan đến tài lộc" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000010"), FormatRhythmName = "Thần tài",   Value = "79",       Description = "Chứa chuỗi số 79 - biểu tượng may mắn" },
                new() { Id = Guid.Parse("f1000000-0000-0000-0000-000000000011"), FormatRhythmName = "Thường",     Value = "NORMAL",   Description = "Số thông thường không thuộc các nhịp đặc biệt" }
            };
            await context.FormatRhythms.AddRangeAsync(rhythms);
            await context.SaveChangesAsync();
        }

        // SimCategories (ca000000-...)
        if (!await context.SimCategories.AnyAsync())
        {
            var categories = new List<SimCategory>
            {
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000001"), CategoryCode = "BAT_QUY",    CategoryName = "SIM Bát quý",   Description = "Đẳng cấp siêu VIP 8 số trùng lặp", ValuationMultiplier = 15.0f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000002"), CategoryCode = "LUC_QUY",    CategoryName = "SIM Lục quý",   Description = "6 chữ số giống nhau liên tiếp", ValuationMultiplier = 10.0f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000003"), CategoryCode = "NGU_QUY",    CategoryName = "SIM Ngũ quý",   Description = "5 chữ số giống nhau liên tiếp", ValuationMultiplier = 6.0f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000004"), CategoryCode = "TU_QUY",     CategoryName = "SIM Tứ quý",    Description = "4 chữ số giống nhau đuôi số", ValuationMultiplier = 3.5f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000005"), CategoryCode = "SANH_TIEN",  CategoryName = "SIM Sảnh tiến", Description = "Chuỗi số tiến đều mang ý nghĩa thăng tiến", ValuationMultiplier = 2.5f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000006"), CategoryCode = "THAN_TAI",   CategoryName = "SIM Thần tài",  Description = "Số đuôi 79, 97 mang lại may mắn", ValuationMultiplier = 2.2f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000007"), CategoryCode = "LOC_PHAT",   CategoryName = "SIM Lộc phát",  Description = "Chứa số 68, 86 phát tài phát lộc", ValuationMultiplier = 2.0f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000008"), CategoryCode = "LAP_DOI",    CategoryName = "SIM Lặp đôi",   Description = "Hai cặp số lặp lại nhịp nhàng", ValuationMultiplier = 2.0f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000009"), CategoryCode = "PHONG_THUY", CategoryName = "SIM Phong thủy",Description = "Chứa nhiều chữ số hợp mệnh 8, 6, 9", ValuationMultiplier = 1.8f },
                new() { Id = Guid.Parse("ca000000-0000-0000-0000-000000000010"), CategoryCode = "SIM_THUONG", CategoryName = "SIM Phổ thông", Description = "Số điện thoại thông thường", ValuationMultiplier = 1.0f }
            };
            await context.SimCategories.AddRangeAsync(categories);
            await context.SaveChangesAsync();
        }

        // SimCarriers (c1000000-...)
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

        // Documents (d1000000-...)
        if (!await context.Documents.AnyAsync())
        {
            var docs = new List<Document>
            {
                new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000001"), DocumentName = "Quy chế Đấu giá Trực tuyến TrustSim", LinkUrl = "https://trustsim.vn/legal/auction-regulation-2026.pdf" },
                new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000002"), DocumentName = "Điều khoản Dịch vụ Ký quỹ Escrow", LinkUrl = "https://trustsim.vn/legal/escrow-terms.pdf" },
                new() { Id = Guid.Parse("d1000000-0000-0000-0000-000000000003"), DocumentName = "Chính sách Xác thực eKYC & Ký số PKI", LinkUrl = "https://trustsim.vn/legal/ekyc-pki-policy.pdf" }
            };
            await context.Documents.AddRangeAsync(docs);
            await context.SaveChangesAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────
    //  2. Level 1: PhonePrefixes (cb000000-...)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedPhonePrefixesAsync(TrustSimDbContext context)
    {
        if (await context.PhonePrefixes.AnyAsync()) return;

        var vttId = Guid.Parse("c1000000-0000-0000-0000-000000000001");
        var vnpId = Guid.Parse("c1000000-0000-0000-0000-000000000002");
        var mbfId = Guid.Parse("c1000000-0000-0000-0000-000000000003");

        var prefixes = new List<PhonePrefix>
        {
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000001"), PrefixValue = "098", SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000002"), PrefixValue = "097", SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000003"), PrefixValue = "086", SimCarrierId = vttId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000004"), PrefixValue = "091", SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000005"), PrefixValue = "088", SimCarrierId = vnpId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000006"), PrefixValue = "090", SimCarrierId = mbfId },
            new() { Id = Guid.Parse("cb000000-0000-0000-0000-000000000007"), PrefixValue = "079", SimCarrierId = mbfId }
        };

        await context.PhonePrefixes.AddRangeAsync(prefixes);
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
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000001"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000001"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000001"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000001"), UserName = "admin",               Email = "admin@trustsim.vn",          Role = UserRole.ADMIN,      FullName = "Tổng Quản Trị Hệ Thống",          Phone = "0900000001", Identity = "001088000001", Dob = new DateTime(1988, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m,          Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000002"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000002"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000002"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000002"), UserName = "consultant_01",       Email = "consultant01@trustsim.vn",  Role = UserRole.CONSULTANT, FullName = "Lê Hoàng Chuyên Viên",              Phone = "0900000002", Identity = "079092000002", Dob = new DateTime(1992, 5, 15, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m,          Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000003"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000003"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000003"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000003"), UserName = "seller_vip_phuquy",   Email = "phuquy.sim@gmail.com",       Role = UserRole.USER,       FullName = "Trần Phú Quý (SIM Phú Quý)",       Phone = "0909999001", Identity = "001085009999", Dob = new DateTime(1985, 10, 20, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 500000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000004"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000004"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000004"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000004"), UserName = "seller_daiphat",      Email = "daiphat.telecom@gmail.com",  Role = UserRole.USER,       FullName = "Nguyễn Đại Phát (Đại Phát Telecom)",Phone = "0912333002", Identity = "079087008888", Dob = new DateTime(1987, 3, 12, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 250000000m, Locked = 0m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000005"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000005"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000005"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000005"), UserName = "bidder_dai_gia",      Email = "hoangnam.invest@gmail.com",  Role = UserRole.USER,       FullName = "Phạm Hoàng Nam (Đầu Tư BĐS)",      Phone = "0988777003", Identity = "001080007777", Dob = new DateTime(1980, 7, 8, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 1100000000m, Locked = 100000000m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000006"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000006"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000006"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000006"), UserName = "bidder_kinh_doanh",  Email = "minhtri.bds@gmail.com",      Role = UserRole.USER,       FullName = "Vũ Minh Trí (Kinh Doanh)",         Phone = "0903444004", Identity = "079090006666", Dob = new DateTime(1990, 11, 25, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 140000000m, Locked = 210000000m, HasPki = true },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000007"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000007"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000007"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000007"), UserName = "bidder_phong_thuy",  Email = "quangduc.fengshui@gmail.com",Role = UserRole.USER,       FullName = "Đặng Quang Đức (Phong Thủy Gia)",  Phone = "0935555005", Identity = "048083005555", Dob = new DateTime(1983, 9, 9, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 170000000m, Locked = 10000000m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000008"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000008"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000008"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000008"), UserName = "user_ekyc_pending",  Email = "thanhha.new@gmail.com",      Role = UserRole.USER,       FullName = "Bùi Thanh Hà",                     Phone = "0977111006", Identity = "001098004444", Dob = new DateTime(1998, 12, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.PENDING_VERIFICATION, Available = 50000000m, Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000009"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000009"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000009"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000009"), UserName = "user_ekyc_rejected", Email = "vietcuong.fake@gmail.com",     Role = UserRole.USER,       FullName = "Ngô Việt Cường",                    Phone = "0944222007", Identity = "040095003333", Dob = new DateTime(1995, 4, 4, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.REJECTED, Available = 0m,          Locked = 0m, HasPki = false },
            new { Id = Guid.Parse("11000000-0000-0000-0000-000000000010"), ProfileId = Guid.Parse("12000000-0000-0000-0000-000000000010"), WalletId = Guid.Parse("14000000-0000-0000-0000-000000000010"), SecurityId = Guid.Parse("13000000-0000-0000-0000-000000000010"), UserName = "user_suspicious_bot", Email = "spambidder99@botmail.com",   Role = UserRole.USER,       FullName = "Bot Nghi Vấn Spam",                Phone = "0966000008", Identity = "001200002222", Dob = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc), Status = VerificationStatus.VERIFIED, Available = 0m,          Locked = 15000000m, HasPki = false }
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
                new() { Id = Guid.Parse("15000000-0000-0000-0000-000000000001"), UserProfileId = profile3, VerifiedByUserId = adminId, FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_001.jpg", BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_001.jpg", FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_001.jpg", FaceSimilarityScore = 99.2f, OcrRawData = "{\"name\":\"Trần Phú Quý\",\"id\":\"001085009999\"}", VerificationMethod = "AUTOMATED_AWS_REKOGNITION", VerificationStatus = VerificationStatus.VERIFIED, SubmittedAt = DateTime.UtcNow.AddDays(-10), VerifiedAt = DateTime.UtcNow.AddDays(-10) },
                new() { Id = Guid.Parse("15000000-0000-0000-0000-000000000002"), UserProfileId = profile8, VerifiedByUserId = null,    FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_008.jpg", BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_008.jpg", FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_008.jpg", FaceSimilarityScore = 78.5f, OcrRawData = "{\"name\":\"Bùi Thanh Hà\",\"id\":\"001098004444\"}", VerificationMethod = "MANUAL_ADMIN_REVIEW", VerificationStatus = VerificationStatus.PENDING_VERIFICATION, SubmittedAt = DateTime.UtcNow.AddHours(-2) },
                new() { Id = Guid.Parse("15000000-0000-0000-0000-000000000003"), UserProfileId = profile9, VerifiedByUserId = adminId, FrontIdCardUrl = "https://storage.trustsim.vn/ekyc/front_009.jpg", BackIdCardUrl = "https://storage.trustsim.vn/ekyc/back_009.jpg", FaceSelfieUrl = "https://storage.trustsim.vn/ekyc/selfie_009.jpg", FaceSimilarityScore = 42.0f, OcrRawData = "{\"name\":\"Ngô Việt Cường\"}", VerificationMethod = "AUTOMATED_AWS_REKOGNITION", VerificationStatus = VerificationStatus.REJECTED, SubmittedAt = DateTime.UtcNow.AddDays(-3), VerifiedAt = DateTime.UtcNow.AddDays(-3), RejectionReason = "Ảnh chụp khuôn mặt không khớp với ảnh trên CCCD (Độ tương đồng 42% < 85%)" }
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

        var pr098 = Guid.Parse("cb000000-0000-0000-0000-000000000001");
        var pr097 = Guid.Parse("cb000000-0000-0000-0000-000000000002");
        var pr091 = Guid.Parse("cb000000-0000-0000-0000-000000000004");
        var pr088 = Guid.Parse("cb000000-0000-0000-0000-000000000005");
        var pr090 = Guid.Parse("cb000000-0000-0000-0000-000000000006");
        var pr079 = Guid.Parse("cb000000-0000-0000-0000-000000000007");

        var fBatQuy = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        var fTuQuy = Guid.Parse("f1000000-0000-0000-0000-000000000004");
        var fSanhTien = Guid.Parse("f1000000-0000-0000-0000-000000000005");
        var fLocPhat = Guid.Parse("f1000000-0000-0000-0000-000000000009");
        var fThanTai = Guid.Parse("f1000000-0000-0000-0000-000000000010");

        var caBatQuy = Guid.Parse("ca000000-0000-0000-0000-000000000001");
        var caTuQuy = Guid.Parse("ca000000-0000-0000-0000-000000000004");
        var caSanhTien = Guid.Parse("ca000000-0000-0000-0000-000000000005");
        var caThanTai = Guid.Parse("ca000000-0000-0000-0000-000000000006");
        var caLocPhat = Guid.Parse("ca000000-0000-0000-0000-000000000007");

        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var sellerDaiPhat = Guid.Parse("11000000-0000-0000-0000-000000000004");

        var sims = new List<SimNumber>
        {
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000001"), PhoneNumber = "0988888888", SimStatus = SimStatus.IN_AUCTION, PhonePrefixId = pr098, FormatRhythmId = fBatQuy },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000002"), PhoneNumber = "0909999999", SimStatus = SimStatus.TRANFERING, PhonePrefixId = pr090, FormatRhythmId = fTuQuy },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000003"), PhoneNumber = "0912345678", SimStatus = SimStatus.IN_AUCTION, PhonePrefixId = pr091, FormatRhythmId = fSanhTien },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000004"), PhoneNumber = "0888686868", SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr088, FormatRhythmId = fLocPhat },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000005"), PhoneNumber = "0797979799", SimStatus = SimStatus.AVAILABLE,  PhonePrefixId = pr079, FormatRhythmId = fThanTai },
            new() { Id = Guid.Parse("21000000-0000-0000-0000-000000000006"), PhoneNumber = "0966668888", SimStatus = SimStatus.TRANFERED,  PhonePrefixId = pr097, FormatRhythmId = fTuQuy },
        };
        await context.SimNumbers.AddRangeAsync(sims);

        var mappings = new List<SimCategoryMapping>
        {
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000001"), SimNumberId = sims[0].Id, SimCategoryId = caBatQuy,   ScoreContribution = 15.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000002"), SimNumberId = sims[1].Id, SimCategoryId = caTuQuy,    ScoreContribution = 3.5f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000003"), SimNumberId = sims[2].Id, SimCategoryId = caSanhTien, ScoreContribution = 2.5f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000004"), SimNumberId = sims[3].Id, SimCategoryId = caLocPhat,  ScoreContribution = 2.0f },
            new() { Id = Guid.Parse("22000000-0000-0000-0000-000000000005"), SimNumberId = sims[4].Id, SimCategoryId = caThanTai,  ScoreContribution = 2.2f }
        };
        await context.SimCategoryMappings.AddRangeAsync(mappings);

        var ownerships = new List<SimOwnership>
        {
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000001"), UserAccountId = sellerPhuQuy, SimNumberId = sims[0].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000002"), UserAccountId = sellerPhuQuy, SimNumberId = sims[1].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000003"), UserAccountId = sellerDaiPhat,SimNumberId = sims[2].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION },
            new() { Id = Guid.Parse("23000000-0000-0000-0000-000000000004"), UserAccountId = sellerDaiPhat,SimNumberId = sims[3].Id, OwnershipStatus = OwnershipStatus.ACTIVE, AcquistionType = AcquistionType.INITIAL_VERIFICATION }
        };
        await context.SimOwnerships.AddRangeAsync(ownerships);

        var verifications = new List<SimVerification>
        {
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000001"), SimOwnershipId = ownerships[0].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-29), VerifiedAt = DateTime.UtcNow.AddDays(-29) },
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000002"), SimOwnershipId = ownerships[1].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-29), VerifiedAt = DateTime.UtcNow.AddDays(-29) },
            new() { Id = Guid.Parse("24000000-0000-0000-0000-000000000003"), SimOwnershipId = ownerships[2].Id, VerificationStatus = VerificationStatus.VERIFIED, RequestedAt = DateTime.UtcNow.AddDays(-19), VerifiedAt = DateTime.UtcNow.AddDays(-19) }
        };
        await context.SimVerifications.AddRangeAsync(verifications);

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  5. Level 4 & 5: Các phiên đấu giá, Bids & Gian lận
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
            new() { Id = Guid.Parse("34000000-0000-0000-0000-000000000002"), AuctionId = auc1.Id, ParticipantId = ap3.Id, BidAmount = 1100000000m, Status = BidStatus.REJECTED,PlacedAt = DateTime.UtcNow.AddHours(-12), RejectedAt = DateTime.UtcNow.AddHours(-12), RejectedReason = "Rate limit exceeded" },
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

        // 7. Auction Transactions & Deposits (37000000-... & 38000000-...)
        var walletBidderDaiGia = await context.Wallets.FirstAsync(w => w.UserAccountId == bidderDaiGia);
        var walletBidderKinhDoanh = await context.Wallets.FirstAsync(w => w.UserAccountId == bidderKinhDoanh);

        var at1 = new AuctionTransaction
        {
            Id = Guid.Parse("37000000-0000-0000-0000-000000000001"),
            WalletId = walletBidderDaiGia.Id,
            TransactionStatus = TransactionStatus.COMPLETED,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_LOCK,
            Amount = 100000000m,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            CompletedAt = DateTime.UtcNow.AddDays(-2),
            BalanceBefore = 1200000000m,
            BalanceAfter = 1100000000m
        };

        var at2 = new AuctionTransaction
        {
            Id = Guid.Parse("37000000-0000-0000-0000-000000000002"),
            WalletId = walletBidderKinhDoanh.Id,
            TransactionStatus = TransactionStatus.COMPLETED,
            AuctionTransactionType = AuctionTransactionType.AUCTION_DEPOSIT_LOCK,
            Amount = 20000000m,
            CreatedAt = DateTime.UtcNow.AddDays(-6),
            CompletedAt = DateTime.UtcNow.AddDays(-6),
            BalanceBefore = 350000000m,
            BalanceAfter = 330000000m
        };

        await context.AuctionTransactions.AddRangeAsync(at1, at2);

        var dep1 = new AuctionDeposit
        {
            Id = Guid.Parse("38000000-0000-0000-0000-000000000001"),
            ParticipantId = ap1.Id,
            DepositCode = "DEP-20260901-001",
            DepositAmount = 100000000m,
            DepositStatus = DepositStatus.LOCKED,
            AuctionTransactionId = at1.Id
        };

        var dep2 = new AuctionDeposit
        {
            Id = Guid.Parse("38000000-0000-0000-0000-000000000002"),
            ParticipantId = ap5.Id,
            DepositCode = "DEP-20260828-002",
            DepositAmount = 20000000m,
            DepositStatus = DepositStatus.APPLIED_TO_ESCROW,
            AppliedtoEscrowedAt = DateTime.UtcNow.AddDays(-2),
            AuctionTransactionId = at2.Id
        };

        await context.AuctionDeposits.AddRangeAsync(dep1, dep2);

        await context.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    //  6. Level 4 -> 6: Giao dịch chuyển nhượng, Hợp đồng & Ký quỹ
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedTradeAndContractsAsync(TrustSimDbContext context)
    {
        if (await context.TradeTransactions.AnyAsync()) return;

        var auc3Id = Guid.Parse("31000000-0000-0000-0000-000000000003");
        var sellerPhuQuy = Guid.Parse("11000000-0000-0000-0000-000000000003");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");
        var sim02 = Guid.Parse("21000000-0000-0000-0000-000000000002");
        var dep2Id = Guid.Parse("38000000-0000-0000-0000-000000000002");

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
            EscrowAmount = 210000000m,
            EscrowStatus = EscrowStatus.HELD,
            HeldAt = DateTime.UtcNow.AddDays(-2),
            AuctionDepositId = dep2Id,
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
    //  7. Level 6: Thông báo & Nhật ký kiểm toán hệ thống
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedNotificationsAndAuditLogsAsync(TrustSimDbContext context)
    {
        if (await context.Notifications.AnyAsync()) return;

        var adminId = Guid.Parse("11000000-0000-0000-0000-000000000001");
        var bidderKinhDoanh = Guid.Parse("11000000-0000-0000-0000-000000000006");
        var userSpamBot = Guid.Parse("11000000-0000-0000-0000-000000000010");

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
