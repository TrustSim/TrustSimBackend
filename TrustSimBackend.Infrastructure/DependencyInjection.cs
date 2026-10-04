using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using TrustSimBackend.Application.Ai.Plugins;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Infrastructure.Services;
using TrustSimBackend.Infrastructure.Services.Ai;
using TrustSimBackend.Infrastructure.Services.Financial;
using TrustSimBackend.Infrastructure.BackgroundServices;
using TrustSimBackend.Application.Features.Auction.Services;

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
        services.Configure<GeminiSettings>(configuration.GetSection(GeminiSettings.SectionName));

        // ─── 2. DbContext ─────────────────────────────────────────────────────────
        // DefaultConnection  → Pooled (PgBouncer) — dùng cho EF Core runtime queries
        services.AddDbContext<TrustSimDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    // Kích hoạt hỗ trợ pgvector trên PostgreSQL
                    npgsql.UseVector();

                    // Cho phép EF Core retry tự động khi Neon compute cold-start (~300ms)
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        // ─── 2.1. ASP.NET Core Identity ──────────────────────────────────────────
        services.AddIdentity<UserAccount, IdentityRole<Guid>>(options =>
        {
            // Password Policy
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequiredLength = 8;

            // Lockout Policy
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.AllowedForNewUsers = true;

            // User Policy
            options.User.RequireUniqueEmail = true;
            options.SignIn.RequireConfirmedEmail = false;
        })
        .AddEntityFrameworkStores<TrustSimDbContext>()
        .AddDefaultTokenProviders();

        // Đăng ký PasswordHasher tùy biến hỗ trợ cả chuẩn Identity mới và tương thích ngược BCrypt cũ
        services.AddScoped<IPasswordHasher<UserAccount>, CustomIdentityPasswordHasher>();

        // ─── 3. UnitOfWork ───────────────────────────────────────────
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // ─── 4. Repositories ─────────────────────────────────────────
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<IIdentityVerificationRepository, IdentityVerificationRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<ISimCarrierRepository, SimCarrierRepository>();
        services.AddScoped<ISimCategoryRepository, SimCategoryRepository>();
        services.AddScoped<ISimCategoryMappingRepository, SimCategoryMappingRepository>();
        services.AddScoped<IPhonePrefixRepository, PhonePrefixRepository>();
        services.AddScoped<ISimNumberRepository, SimNumberRepository>();
        services.AddScoped<ISimRepository, SimRepository>();
        services.AddScoped<ISimOwnershipRepository, SimOwnershipRepository>();
        services.AddScoped<ISimVerificationRepository, SimVerificationRepository>();
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

        // AI & RAG Repositories
        services.AddScoped<IDocumentChunkRepository, DocumentChunkRepository>();
        services.AddScoped<ISimValuationRepository, SimValuationRepository>();
        services.AddScoped<IAiConsultationSessionRepository, AiConsultationSessionRepository>();

        // Financial Double-Entry Ledger Repositories
        services.AddScoped<ILedgerJournalRepository, LedgerJournalRepository>();
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();
        services.AddScoped<IPaymentIntentRepository, PaymentIntentRepository>();
        services.AddScoped<IEscrowRepository, EscrowRepository>();
        services.AddScoped<IAuctionTransactionRepository, AuctionTransactionRepository>();

        // ─── 5. Infrastructure Services ──────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<ITelcoVerificationService, MockTelcoVerificationService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICookieService, CookieService>();
        services.AddScoped<ISePayService, SePayService>();
        services.AddSingleton<ITokenBlacklistService, TokenBlacklistService>();

        // Financial Integrity & Cryptographic Ledger Engine
        services.AddSingleton<IFinancialSigningService, EcdsaFinancialSigningService>();
        services.AddScoped<ILedgerService, DoubleEntryLedgerService>();
        services.AddScoped<ILedgerIntegrityVerifier, LedgerIntegrityVerifier>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IMockBankPayoutGatewayService, MockBankPayoutGatewayService>();
        services.AddHostedService<LedgerIntegrityCheckWorker>();

        // Real-time Auction Delay Queue & Dispatcher Engine (Redis ZSET + In-Memory Timer)
        services.AddSingleton<IAuctionDelayQueueService, RedisDelayQueueService>();
        services.AddHostedService<AuctionDelayQueueDispatcher>();

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

        // ─── 10. AI & RAG Pipeline Services (Semantic Kernel & Gemini) ───────────
        services.AddScoped<ISimFeatureExtractor, SimFeatureExtractor>();
        services.AddScoped<ISimValuationMathEngine, SimValuationMathEngine>();
        services.AddScoped<ITextChunkerService, TextChunkerService>();
        services.AddHttpClient<IWebCrawlerService, WebCrawlerService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(25);
        });

        // 9 Native Semantic Kernel Plugins
        services.AddScoped<SimInventoryPlugin>();
        services.AddScoped<FengShuiKnowledgePlugin>();
        services.AddScoped<SimCadencePlugin>();
        services.AddScoped<AuctionPlugin>();
        services.AddScoped<TradeAndEscrowPlugin>();
        services.AddScoped<UserWalletPlugin>();
        services.AddScoped<SimValuationPlugin>();
        services.AddScoped<SimMarketHistoryPlugin>();
        services.AddScoped<CarrierPolicyPlugin>();

        // Semantic Kernel Core Registration
        services.AddTransient<Kernel>(sp =>
        {
            var geminiOptions = sp.GetRequiredService<IOptions<GeminiSettings>>().Value;
            var builder = Kernel.CreateBuilder();

            if (!string.IsNullOrWhiteSpace(geminiOptions.ApiKey))
            {
                var endpointUri = !string.IsNullOrWhiteSpace(geminiOptions.BaseUrl)
                    ? new Uri(geminiOptions.BaseUrl)
                    : new Uri("https://generativelanguage.googleapis.com/v1beta/openai/");

                var chatModel = !string.IsNullOrWhiteSpace(geminiOptions.ChatModel)
                    ? geminiOptions.ChatModel
                    : "gemini-3.1-flash-lite";

#pragma warning disable SKEXP0010
                builder.AddOpenAIChatCompletion(
                    modelId: chatModel,
                    apiKey: geminiOptions.ApiKey,
                    endpoint: endpointUri
                );
#pragma warning restore SKEXP0010
            }

            // Đăng ký 9 Native Plugins vào Kernel
            builder.Plugins.AddFromObject(sp.GetRequiredService<SimInventoryPlugin>(), "SimInventoryPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<FengShuiKnowledgePlugin>(), "FengShuiKnowledgePlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<SimCadencePlugin>(), "SimCadencePlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<AuctionPlugin>(), "AuctionPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<TradeAndEscrowPlugin>(), "TradeAndEscrowPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<UserWalletPlugin>(), "UserWalletPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<SimValuationPlugin>(), "SimValuationPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<SimMarketHistoryPlugin>(), "SimMarketHistoryPlugin");
            builder.Plugins.AddFromObject(sp.GetRequiredService<CarrierPolicyPlugin>(), "CarrierPolicyPlugin");

            return builder.Build();
        });

        services.AddHttpClient<IGeminiEmbeddingService, GeminiEmbeddingService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<ISemanticKernelAgentService, SemanticKernelAgentService>();
        services.AddScoped<IRagRetrievalService, RagRetrievalService>();
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        return services;
    }
}
