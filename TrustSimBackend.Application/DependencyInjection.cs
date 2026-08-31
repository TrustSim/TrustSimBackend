using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TrustSimBackend.Application.Common.Behaviors;

namespace TrustSimBackend.Application;

/// <summary>
/// Extension method để đăng ký toàn bộ dịch vụ của tầng Application vào DI container:
/// - MediatR (tất cả Command/Query Handlers)
/// - Pipeline Behaviors (ValidationBehavior)
/// - FluentValidation (tất cả Validators)
/// - AutoMapper (tất cả Mapping Profiles)
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // 1. Đăng ký AutoMapper quét tất cả Profile trong Application assembly
        services.AddAutoMapper(assembly);

        // 2. Đăng ký tất cả Validator của FluentValidation trong Application assembly
        services.AddValidatorsFromAssembly(assembly);

        // 3. Đăng ký MediatR và tích hợp Pipeline Behavior
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            
            // Tự động validate dữ liệu bằng FluentValidation trước khi chuyển đến Handler
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // 4. Đăng ký Factory & Strategies cho Auction (Strategy & Factory Patterns)
        services.AddScoped<Features.Auction.Strategies.IAuctionStrategy, Features.Auction.Strategies.EnglishAuctionStrategy>();
        services.AddScoped<Features.Auction.Strategies.IAuctionStrategy, Features.Auction.Strategies.DutchAuctionStrategy>();
        services.AddScoped<Features.Auction.Factory.AuctionFactory>();

        return services;
    }
}
