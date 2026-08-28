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
        // ─── 1. DbContext ────────────────────────────────────────────
        services.AddDbContext<TrustSimDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        });

        // ─── 2. UnitOfWork ───────────────────────────────────────────
        // Chuyển tiếp IUnitOfWork về cùng 1 instance TrustSimDbContext trong request (tránh tạo 2 DbContext riêng)
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<TrustSimDbContext>());

        // ─── 3. Repositories ─────────────────────────────────────────
        services.AddScoped<ITradeTransactionRepository, TradeTransactionRepository>();
        services.AddScoped<IUserAccountRepository, UserAccountRepository>();

        // ─── 4. Infrastructure Services ──────────────────────────────
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        return services;
    }
}
