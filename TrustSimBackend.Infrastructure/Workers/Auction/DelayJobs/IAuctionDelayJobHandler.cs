using TrustSimBackend.Application.Features.Auction.Services;

namespace TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;

/// <summary>
/// Contract xử lý từng loại tác vụ trễ trong vòng đời phiên đấu giá.
/// </summary>
public interface IAuctionDelayJobHandler
{
    bool CanHandle(string jobType);
    Task HandleAsync(AuctionDelayJob job, CancellationToken cancellationToken);
}
