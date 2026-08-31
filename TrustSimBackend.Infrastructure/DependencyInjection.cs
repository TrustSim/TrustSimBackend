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
        // ─── 1. DbContext ─────────────────────────────────────────────────────────
        // DefaultConnection  → Pooled (PgBouncer) — dùng cho EF Core runtime queries
        // MigrationsConnection → Direct (unpooled)  — dùng cho dotnet ef migrations
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

        // ─── 2. UnitOfWork ───────────────────────────────────────────
        // Chuyển tiếp IUnitOfWork về cùng 1 instance TrustSimDbContext trong request (tránh tạo 2 DbContext riêng)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TrustSimDbContext>());

        // ─── 3. Repositories ─────────────────────────────────────────
        services.AddScoped<ITradeTransactionRepository, TradeTransactionRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();
        services.AddScoped<ISimMasterDataRepository, SimMasterDataRepository>();
        services.AddScoped<ISimRepository, SimRepository>();
        services.AddScoped<IAuctionRepository, AuctionRepository>();

        // ─── 4. Infrastructure Services ──────────────────────────────
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        // ─── 5. Background Hosted Services ───────────────────────────
        services.AddHostedService<BackgroundServices.AuctionActivationService>();

        return services;
    }
}
