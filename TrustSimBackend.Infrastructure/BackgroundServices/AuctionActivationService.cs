using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Features.Auction.Commands.ActivateAuction;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.BackgroundServices;

/// <summary>
/// Background Service tự động kích hoạt phiên đấu giá khi đến giờ StartAt.
/// 
/// Cách hoạt động:
/// - Chạy định kỳ mỗi CheckIntervalSeconds (mặc định: 60 giây).
/// - Quét DB tìm tất cả phiên SCHEDULE có StartAt &lt;= DateTime.UtcNow.
/// - Gọi ActivateAuctionCommand qua MediatR cho từng phiên tìm thấy.
/// - State Pattern trong Command Handler đảm bảo tính đúng đắn của chuyển trạng thái.
/// 
/// Lưu ý: Sử dụng IServiceScopeFactory để tạo scope mới cho mỗi lần quét,
/// tránh lỗi DbContext bị dispose khi dùng trong Singleton BackgroundService.
/// </summary>
public class AuctionActivationService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionActivationService> _logger;

    // Chu kỳ quét (giây) — có thể đọc từ IConfiguration nếu cần
    private const int CheckIntervalSeconds = 60;

    public AuctionActivationService(
        IServiceScopeFactory scopeFactory,
        ILogger<AuctionActivationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[AuctionActivation] Background service started. Checking every {interval}s.",
            CheckIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndActivateAuctions(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[AuctionActivation] Lỗi khi quét phiên đấu giá cần kích hoạt.");
            }

            await Task.Delay(TimeSpan.FromSeconds(CheckIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("[AuctionActivation] Background service stopped.");
    }

    private async Task CheckAndActivateAuctions(CancellationToken cancellationToken)
    {
        // Tạo scope riêng để tránh sharing DbContext với các request khác
        using var scope = _scopeFactory.CreateScope();
        var auctionRepository = scope.ServiceProvider.GetRequiredService<IAuctionRepository>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var now = DateTime.UtcNow;

        // Tìm tất cả phiên SCHEDULE đã đến giờ bắt đầu
        var (pendingAuctions, _) = await auctionRepository.GetAdminPagedAsync(
            pageIndex: 1,
            pageSize: 100, // Xử lý tối đa 100 phiên / lần quét
            auctionType: null,
            status: AuctionStatus.SCHEDULE,
            authorId: null,
            search: null,
            startFrom: null,
            startTo: now, // StartAt <= now
            cancellationToken: cancellationToken);

        if (!pendingAuctions.Any())
            return;

        _logger.LogInformation(
            "[AuctionActivation] Tìm thấy {count} phiên đấu giá cần kích hoạt lúc {time}.",
            pendingAuctions.Count, now.ToString("HH:mm:ss dd/MM/yyyy"));

        // Kích hoạt từng phiên qua MediatR (tận dụng State Pattern trong ActivateAuctionCommand)
        foreach (var auction in pendingAuctions)
        {
            try
            {
                await mediator.Send(new ActivateAuctionCommand(auction.Id), cancellationToken);
                _logger.LogInformation(
                    "[AuctionActivation] ✅ Phiên '{code}' (ID: {id}) đã được kích hoạt tự động.",
                    auction.AuctionCode, auction.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[AuctionActivation] ❌ Lỗi khi kích hoạt phiên '{code}' (ID: {id}).",
                    auction.AuctionCode, auction.Id);
                // Tiếp tục với phiên tiếp theo, không dừng toàn bộ batch
            }
        }
    }
}
