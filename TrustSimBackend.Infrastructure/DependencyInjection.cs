using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Repositories;

namespace TrustSimBackend.Infrastructure;

/// <summary>
/// Extension method để đăng ký toàn bộ dịch vụ tầng Infrastructure vào DI Container.
/// Gọi builder.Services.AddInfrastructureServices(builder.Configuration) trong Program.cs.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── 1. DbContext ────────────────────────────────────────────
        // Đăng ký TrustSimDbContext với chuỗi kết nối PostgreSQL từ appsettings.json
        services.AddDbContext<TrustSimDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
        });

        // ─── 2. UnitOfWork ───────────────────────────────────────────
        // DbContext đóng vai trò UnitOfWork — dùng Scoped để mỗi HTTP request có 1 instance riêng
        services.AddScoped<IUnitOfWork, TrustSimDbContext>();

        // ─── 3. Repositories ─────────────────────────────────────────
        // Thêm các Repository mới vào đây khi bạn implement chúng
        services.AddScoped<ITradeTransactionRepository, TradeTransactionRepository>();

        return services;
    }
}
