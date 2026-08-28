using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence;

/// <summary>
/// Nạp dữ liệu ban đầu (Master Data) vào Database khi ứng dụng khởi động lần đầu.
/// Mọi phương thức đều kiểm tra AnyAsync() trước → đảm bảo chạy lại nhiều lần không bị lỗi (Idempotent).
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(TrustSimDbContext context)
    {
        // 1. Seed dữ liệu bảng tra cứu cơ bản (không phụ thuộc gì khác)
        await SeedFormatRhythmsAsync(context);
        await SeedSimCategoriesAsync(context);

        // 2. Seed Nhà mạng trước → sau đó mới Seed Đầu số (phụ thuộc vào SimCarrierId)
        await SeedSimCarriersAsync(context);
        await SeedPhonePrefixesAsync(context);

        // 3. Cuối cùng mới Seed Admin (phụ thuộc vào dữ liệu sạch của các bảng trên)
        await SeedAdminUserAsync(context);

        // Lưu toàn bộ thay đổi vào DB một lần duy nhất (hiệu quả hơn SaveChanges lẻ)
        await context.SaveChangesAsync();

        Console.WriteLine("[DbSeeder] ✅ Seed data hoàn tất.");
    }

    // ─────────────────────────────────────────────────────────────
    //  1. FormatRhythm — Nhịp số phân loại SIM
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedFormatRhythmsAsync(TrustSimDbContext context)
    {
        if (await context.FormatRhythms.AnyAsync()) return;

        var rhythms = new List<FormatRhythm>
        {
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Tứ quý",     Value = "AAAA",      Description = "4 chữ số giống nhau liên tiếp. VD: 0909 9999" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Ngũ quý",    Value = "AAAAA",     Description = "5 chữ số giống nhau liên tiếp. VD: 0988 88888" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Lục quý",    Value = "AAAAAA",    Description = "6 chữ số giống nhau liên tiếp. VD: 090 999999" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Sảnh tiến",  Value = "ABCDE",     Description = "5 chữ số tăng dần liên tiếp. VD: 090 12345" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Sảnh lùi",   Value = "EDCBA",     Description = "5 chữ số giảm dần liên tiếp. VD: 090 98765" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Lặp đôi",    Value = "ABABAB",    Description = "Hai chữ số xen kẽ nhau. VD: 0909 090909" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Gánh đảo",   Value = "ABBA",      Description = "4 số đối xứng. VD: 0909 1221" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Tam hoa",    Value = "AAABBB",    Description = "3 số A + 3 số B. VD: 0909 000111" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Lộc phát",   Value = "68",        Description = "Chứa cặp số 68 hoặc 86 liên quan đến tài lộc" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Thần tài",   Value = "79",        Description = "Chứa chuỗi số 79 — con số của sự may mắn" },
            new() { Id = Guid.NewGuid(), FormatRhythmName = "Thường",     Value = "ABCDE",     Description = "Số thông thường, không thuộc các nhịp đặc biệt" },
        };

        await context.FormatRhythms.AddRangeAsync(rhythms);
        Console.WriteLine($"[DbSeeder] Đã thêm {rhythms.Count} FormatRhythm.");
    }

    // ─────────────────────────────────────────────────────────────
    //  2. SimCategory — Danh mục phân loại SIM phong thủy
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedSimCategoriesAsync(TrustSimDbContext context)
    {
        if (await context.SimCategories.AnyAsync()) return;

        var categories = new List<SimCategory>
        {
            new() { Id = Guid.NewGuid(), CategoryCode = "TU_QUY",       CategoryName = "SIM Tứ quý",      Description = "4 chữ số giống nhau cuối số. VD: 9999, 8888",          ValuationMultiplier = 3.5f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "NGU_QUY",      CategoryName = "SIM Ngũ quý",     Description = "5 chữ số giống nhau liên tiếp. VD: 99999, 88888",       ValuationMultiplier = 6.0f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "LUC_QUY",      CategoryName = "SIM Lục quý",     Description = "6 chữ số giống nhau liên tiếp. VD: 888888",             ValuationMultiplier = 10.0f },
            new() { Id = Guid.NewGuid(), CategoryCode = "SANH_TIEN",    CategoryName = "SIM Sảnh tiến",   Description = "Chuỗi số tăng dần. VD: 12345, 23456",                  ValuationMultiplier = 2.5f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "LOC_PHAT",     CategoryName = "SIM Lộc phát",    Description = "Chứa số 68, 86, 6868, 8686 liên quan tài lộc",          ValuationMultiplier = 2.0f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "THAN_TAI",     CategoryName = "SIM Thần tài",    Description = "Số đuôi 79, 97, 7979 — mang lại may mắn",              ValuationMultiplier = 2.2f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "PHONG_THUY",   CategoryName = "SIM Phong thủy",  Description = "Số có ý nghĩa phong thủy cao: chứa nhiều số 8, 6, 9",   ValuationMultiplier = 1.8f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "LAP_DOI",      CategoryName = "SIM Lặp đôi",     Description = "Hai chữ số xen kẽ luân phiên. VD: 090909, 686868",     ValuationMultiplier = 2.0f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "GANH_DAO",     CategoryName = "SIM Gánh đảo",    Description = "Số đối xứng. VD: 1221, 3443",                           ValuationMultiplier = 1.5f  },
            new() { Id = Guid.NewGuid(), CategoryCode = "SIM_THUONG",   CategoryName = "SIM Thường",      Description = "Số không thuộc danh mục đặc biệt nào",                  ValuationMultiplier = 1.0f  },
        };

        await context.SimCategories.AddRangeAsync(categories);
        Console.WriteLine($"[DbSeeder] Đã thêm {categories.Count} SimCategory.");
    }

    // ─────────────────────────────────────────────────────────────
    //  3. SimCarrier — Nhà mạng viễn thông
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedSimCarriersAsync(TrustSimDbContext context)
    {
        if (await context.SimCarriers.AnyAsync()) return;

        var carriers = new List<SimCarrier>
        {
            new() { Id = Guid.NewGuid(), CarrierName = "Viettel Telecom",      CarrierCode = "VTT", LogoImg = "https://assets.trustsim.vn/carriers/viettel.png" },
            new() { Id = Guid.NewGuid(), CarrierName = "VinaPhone",            CarrierCode = "VNP", LogoImg = "https://assets.trustsim.vn/carriers/vinaphone.png" },
            new() { Id = Guid.NewGuid(), CarrierName = "MobiFone",             CarrierCode = "MBF", LogoImg = "https://assets.trustsim.vn/carriers/mobifone.png" },
            new() { Id = Guid.NewGuid(), CarrierName = "Vietnamobile",         CarrierCode = "VNM", LogoImg = "https://assets.trustsim.vn/carriers/vietnamobile.png" },
            new() { Id = Guid.NewGuid(), CarrierName = "Reddi (Wintel)",       CarrierCode = "RDD", LogoImg = "https://assets.trustsim.vn/carriers/reddi.png" },
            new() { Id = Guid.NewGuid(), CarrierName = "Indochina Telecom",    CarrierCode = "ICT", LogoImg = "https://assets.trustsim.vn/carriers/itelecom.png" },
        };

        await context.SimCarriers.AddRangeAsync(carriers);
        Console.WriteLine($"[DbSeeder] Đã thêm {carriers.Count} SimCarrier.");
    }

    // ─────────────────────────────────────────────────────────────
    //  4. PhonePrefix — Đầu số (phụ thuộc SimCarrier đã tồn tại)
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedPhonePrefixesAsync(TrustSimDbContext context)
    {
        if (await context.PhonePrefixes.AnyAsync()) return;

        // Lấy các nhà mạng từ DB (đã được Seed ở bước trên, chưa SaveChanges nên dùng Local)
        var carriers = context.SimCarriers.Local;

        var vtt = carriers.First(c => c.CarrierCode == "VTT");
        var vnp = carriers.First(c => c.CarrierCode == "VNP");
        var mbf = carriers.First(c => c.CarrierCode == "MBF");
        var vnm = carriers.First(c => c.CarrierCode == "VNM");
        var rdd = carriers.First(c => c.CarrierCode == "RDD");

        var prefixes = new List<PhonePrefix>
        {
            // Viettel
            new() { Id = Guid.NewGuid(), PrefixValue = "032", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "033", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "034", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "035", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "036", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "038", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "086", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "096", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "097", SimCarrierId = vtt.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "098", SimCarrierId = vtt.Id },
            // VinaPhone
            new() { Id = Guid.NewGuid(), PrefixValue = "081", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "082", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "083", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "084", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "085", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "088", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "091", SimCarrierId = vnp.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "094", SimCarrierId = vnp.Id },
            // MobiFone
            new() { Id = Guid.NewGuid(), PrefixValue = "070", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "076", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "077", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "078", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "079", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "089", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "090", SimCarrierId = mbf.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "093", SimCarrierId = mbf.Id },
            // Vietnamobile
            new() { Id = Guid.NewGuid(), PrefixValue = "052", SimCarrierId = vnm.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "056", SimCarrierId = vnm.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "058", SimCarrierId = vnm.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "092", SimCarrierId = vnm.Id },
            // Reddi/Wintel
            new() { Id = Guid.NewGuid(), PrefixValue = "055", SimCarrierId = rdd.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "059", SimCarrierId = rdd.Id },
            new() { Id = Guid.NewGuid(), PrefixValue = "099", SimCarrierId = rdd.Id },
        };

        await context.PhonePrefixes.AddRangeAsync(prefixes);
        Console.WriteLine($"[DbSeeder] Đã thêm {prefixes.Count} PhonePrefix.");
    }

    // ─────────────────────────────────────────────────────────────
    //  5. Admin User mặc định
    // ─────────────────────────────────────────────────────────────
    private static async Task SeedAdminUserAsync(TrustSimDbContext context)
    {
        // Kiểm tra theo Role ADMIN
        if (await context.UserAccounts.AnyAsync(u => u.Role == UserRole.ADMIN)) return;

        var adminId = Guid.NewGuid();

        var admin = new UserAccount
        {
            Id          = adminId,
            UserName    = "admin",
            UserEmail   = "admin@trustsim.vn",
            // NOTE: Đây là hash BCrypt của password "Admin@123456"
            // Trong production, hãy thay bằng hash thực từ BCrypt.HashPassword(...)
            PassWordHash = "$2a$12$SampleHashAdminTrustSimPleaseChangeThis",
            Role        = UserRole.ADMIN,
            IsActive    = true,
        };

        var adminProfile = new UserProfile
        {
            Id            = Guid.NewGuid(),
            UserAccountId = adminId,
            FullName      = "Quản trị viên TrustSim",
            DateOfBirth   = new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Email         = "admin@trustsim.vn",
            PhoneNumber   = "0900000000",
            Address       = "Hà Nội, Việt Nam",
            IdentityNumber= "000000000000",
            AvatarImg     = "https://assets.trustsim.vn/avatars/admin.png",
            VerificationStatus = VerificationStatus.VERIFIED,
        };

        var adminWallet = new Wallet
        {
            Id               = Guid.NewGuid(),
            UserAccountId    = adminId,
            AvailableBalance = 0,
            LockedBalance    = 0,
            WalletStatus     = WalletStatus.ACTIVE,
            LockVersion      = 0,
        };

        admin.UserProfile = adminProfile;
        admin.Wallet = adminWallet;

        await context.UserAccounts.AddAsync(admin);

        Console.WriteLine("[DbSeeder] Đã thêm tài khoản Admin mặc định.");
    }
}
