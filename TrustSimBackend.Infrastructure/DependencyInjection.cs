using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Infrastructure.Services;

namespace TrustSimBackend.Infrastructure;

/// <summary>
/// Extension method để đăng ký toàn bộ dịch vụ tầng Infrastructure vào DI Container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── 1. Cấu hình Option Settings ──────────────────────────────────────────
        services.Configure<SePaySettings>(configuration.GetSection(SePaySettings.SectionName));
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.Configure<SpeedSmsSettings>(configuration.GetSection(SpeedSmsSettings.SectionName));
        services.Configure<AwsRekognitionSettings>(configuration.GetSection(AwsRekognitionSettings.SectionName));

        // ─── 2. DbContext ─────────────────────────────────────────────────────────
        // DefaultConnection  → Pooled (PgBouncer) — dùng cho EF Core runtime queries
        services.AddDbContext<TrustSimDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    // Cho phép EF Core retry tự động khi Neon compute cold-start (~300ms)
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        // ─── 3. UnitOfWork ───────────────────────────────────────────
        // Chuyển tiếp IUnitOfWork về cùng 1 instance TrustSimDbContext trong request (tránh tạo 2 DbContext riêng)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TrustSimDbContext>());

        // ─── 4. Repositories ─────────────────────────────────────────
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<ISimCarrierRepository, SimCarrierRepository>();
        services.AddScoped<ISimCategoryRepository, SimCategoryRepository>();
        services.AddScoped<IPhonePrefixRepository, PhonePrefixRepository>();
        services.AddScoped<ISimNumberRepository, SimNumberRepository>();
        services.AddScoped<ISimRepository, SimRepository>();
        services.AddScoped<ISimMasterDataRepository, SimMasterDataRepository>();
        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<IBidRepository, BidRepository>();
        services.AddScoped<ITradeTransactionRepository, TradeTransactionRepository>();
        services.AddScoped<IDisputeTicketRepository, DisputeTicketRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IFormatRhythmRepository, FormatRhythmRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        // ─── 5. Infrastructure Services ──────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICookieService, CookieService>();
        services.AddScoped<ISePayService, SePayService>();

        // ─── 6. Bulk Operations & File Services (CSV/Excel) ──────────
        services.AddScoped<IBulkDataService, BulkDataService>();
        services.AddScoped<ICsvService, CsvService>();
        services.AddScoped<IExcelService, ExcelService>();

        // ─── 7. Email Service ────────────────────────────────────────
        services.AddScoped<IEmailService, EmailService>();

        // ─── 8. SpeedSMS & OTP Services ──────────────────────────────
        services.AddHttpClient<ISpeedSmsService, SpeedSmsService>();
        services.AddScoped<IOtpService, OtpService>();

        // ─── 9. AWS Rekognition & Storage Services (eKYC) ────────────
        services.AddScoped<IAwsRekognitionService, AwsRekognitionService>();
        services.AddScoped<ICloudStorageService, CloudStorageService>();
        services.AddScoped<IPkiKeyService, PkiKeyService>();

        return services;
    }
}
