using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Commands;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Queries;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Infrastructure.Services;
using TrustSimBackend.Infrastructure.Workers.Auction;
using TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;
using TrustSimBackend.Tests.Common;
using Xunit;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;

namespace TrustSimBackend.Tests.UnitTests.Auctions;

public class AuctionRealtimeActivationAndWorkerTests
{
    private static RedisDelayQueueService CreateInMemoryDelayQueueService()
    {
        var config = new ConfigurationBuilder().Build();
        return new RedisDelayQueueService(config, NullLogger<RedisDelayQueueService>.Instance, redis: null);
    }

    private static (AuctionEntity Auction, AuctionParticipant Participant, Wallet Wallet) CreateAuctionFixture(
        TestTrustSimDbContext context,
        AuctionStatus status,
        DateTime startAt,
        DateTime endAt,
        AuctionType auctionType = AuctionType.ENGLISH)
    {
        var carrierId = Guid.NewGuid();
        var prefixId = Guid.NewGuid();
        var simId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var bidderId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();

        var carrier = new SimCarrier { Id = carrierId, CarrierCode = "VT", CarrierName = "Viettel" };
        var prefix = new PhonePrefix { Id = prefixId, PrefixValue = "098", SimCarrierId = carrierId, SimCarrier = carrier };
        var sim = new SimNumber { Id = simId, PhoneNumber = "0988888888", PhonePrefixId = prefixId, PhonePrefix = prefix, SimStatus = SimStatus.IN_AUCTION };

        var seller = new UserAccount { Id = sellerId, UserName = "seller_test", Email = "seller@test.local" };
        var bidder = new UserAccount { Id = bidderId, UserName = "bidder_test", Email = "bidder@test.local" };
        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = bidderId,
            AvailableBalance = 500_000_000m,
            LockedBalance = 50_000_000m,
            WalletStatus = WalletStatus.ACTIVE
        };
        bidder.Wallet = wallet;

        var rule = new AuctionRule
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            MinimumBidIncrement = 1_000_000m,
            MaxStepMultiplier = 10,
            DepositAmount = 50_000_000m,
            MarginPercentage = 0.10m,
            ExtensionThresholdSeconds = 30,
            ExtensionDurationSeconds = 30,
            MaxSnipeExtensionsPerParticipant = 3,
            DutchDecreaseAmount = 2_000_000m,
            DutchDecreaseIntervalSeconds = 60,
            IsActive = true
        };

        var participant = new AuctionParticipant
        {
            Id = participantId,
            AuctionId = auctionId,
            UserAccountId = bidderId,
            UserAccount = bidder,
            Status = ParticipantStatus.REGISTERED,
            JoinedAt = DateTime.UtcNow.AddMinutes(-5)
        };
        participant.Deposits.Add(new AuctionDeposit
        {
            Id = Guid.NewGuid(),
            ParticipantId = participantId,
            DepositCode = $"DEP-{Guid.NewGuid():N}"[..12],
            DepositAmount = 50_000_000m,
            DepositType = DepositType.INITIAL,
            DepositStatus = DepositStatus.LOCKED
        });

        var auction = new AuctionEntity
        {
            Id = auctionId,
            AuctionCode = $"AUC-{Guid.NewGuid():N}"[..12],
            AuctionType = auctionType,
            AuctionStatus = status,
            StartingPrice = 100_000_000m,
            CurrentPrice = 100_000_000m,
            ReservePrice = auctionType == AuctionType.DUTCH ? 60_000_000m : 0m,
            StartAt = startAt,
            EndAt = endAt,
            RegistrationStartAt = startAt.AddHours(-2),
            RegistrationEndAt = startAt,
            AuthorId = sellerId,
            Author = seller,
            SimNumberId = simId,
            SimNumber = sim,
            AuctionRule = rule,
            LockVersion = 1,
            IsActive = true
        };
        auction.Participants.Add(participant);

        context.SimCarriers.Add(carrier);
        context.PhonePrefixes.Add(prefix);
        context.SimNumbers.Add(sim);
        context.Users.AddRange(seller, bidder);
        context.Wallets.Add(wallet);
        context.Auctions.Add(auction);
        context.SaveChanges();

        return (auction, participant, wallet);
    }

    [Fact]
    public void RedisAuctionCacheService_LoadLuaScript_ShouldLoadEmbeddedPlaceBidScriptWithJitAndCacheMissGuards()
    {
        var lua = RedisAuctionCacheService.LoadLuaScript();

        lua.Should().NotBeNullOrWhiteSpace();
        lua.Should().Contain("ERR_AUCTION_CACHE_MISS");
        lua.Should().Contain("ERR_PARTICIPANT_CACHE_MISS");
        lua.Should().Contain("startAtMs");
        lua.Should().Contain("participantCount");
        lua.Should().Contain("justActivated");
    }

    [Fact]
    public async Task RedisDelayQueueService_ShouldRouteTwoLanesSeparatelyAndWakeUpImmediatelyOnEnqueue()
    {
        var queue = CreateInMemoryDelayQueueService();
        var liveAuctionId = Guid.NewGuid();
        var postAuctionId = Guid.NewGuid();

        var dueTime = DateTime.UtcNow.AddMilliseconds(40);
        await queue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = postAuctionId,
            JobType = AuctionDelayJobTypes.PAYMENT_DEADLINE,
            ExecuteAt = dueTime
        });

        // Khởi chạy tác vụ chờ tối đa 10s trên Live lane; đẩy job Live sau đó phải đánh thức ngay tức thì
        var waitTask = queue.WaitForSignalOrDelayAsync(AuctionDelayQueueLane.Live, TimeSpan.FromSeconds(10));

        await Task.Delay(30);
        await queue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = liveAuctionId,
            JobType = AuctionDelayJobTypes.AUCTION_PRE_WARM,
            ExecuteAt = DateTime.UtcNow.AddSeconds(30)
        });

        var completed = await Task.WhenAny(waitTask, Task.Delay(1500));
        completed.Should().Be(waitTask, "Live lane worker must wake up immediately instead of waiting full timeout.");

        // Xác nhận pop đúng từng lane độc lập
        await Task.Delay(60);
        var liveDue = await queue.PopNextDueJobAsync(DateTime.UtcNow, AuctionDelayQueueLane.Live);
        liveDue.Should().BeNull("Pre-warm is still 30s in the future.");

        var postDue = await queue.PopNextDueJobAsync(DateTime.UtcNow, AuctionDelayQueueLane.PostAuction);
        postDue.Should().NotBeNull();
        postDue!.AuctionId.Should().Be(postAuctionId);
        postDue.JobType.Should().Be(AuctionDelayJobTypes.PAYMENT_DEADLINE);

        await queue.CancelJobAsync(liveAuctionId, AuctionDelayJobTypes.AUCTION_PRE_WARM);
        var nextLiveAfterCancel = await queue.GetNextJobTimeAsync(AuctionDelayQueueLane.Live);
        nextLiveAfterCancel.Should().BeNull();
    }

    [Fact]
    public async Task PlaceBidCommandHandler_WhenRedisCacheMissInsideActiveWindow_ShouldWarmUpJitActivateAndBroadcast()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (auction, participant, wallet) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddSeconds(-2),
            DateTime.UtcNow.AddMinutes(30));

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Auctions).Returns(new AuctionRepository(context));
        mockUow.Setup(u => u.Bids).Returns(new BidRepository(context));
        mockUow.Setup(u => u.Wallets).Returns(new WalletRepository(context));
        mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => context.SaveChangesAsync(ct));

        var mockRedis = new Mock<IRedisAuctionCacheService>();
        var callCount = 0;
        mockRedis.Setup(r => r.PlaceBidAtomicAsync(
                auction.Id,
                participant.Id,
                participant.UserAccountId,
                105_000_000m,
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid aId, Guid pId, Guid uId, decimal amt, Guid bId, CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult(new LuaBidResult(
                        false, "ERR_AUCTION_CACHE_MISS", "Missing state",
                        bId, aId, pId, uId, amt, 0, 0, false, 0));
                }

                return Task.FromResult(new LuaBidResult(
                    true, null, null,
                    bId, aId, pId, uId, amt, amt,
                    new DateTimeOffset(auction.EndAt).ToUnixTimeMilliseconds(),
                    false, 2, JustActivated: true));
            });

        var mockNotifier = new Mock<IAuctionRealtimeNotifier>();
        var mockDelayQueue = new Mock<IAuctionDelayQueueService>();

        var handler = new PlaceBidCommandHandler(
            mockUow.Object,
            mockRedis.Object,
            mockNotifier.Object,
            mockDelayQueue.Object);

        var bidDto = await handler.Handle(
            new PlaceBidCommand(105_000_000m, auction.Id, participant.UserAccountId),
            CancellationToken.None);

        bidDto.BidAmount.Should().Be(105_000_000m);

        // Cache miss đã được tự nạp lại qua WarmUpAuctionAsync
        mockRedis.Verify(r => r.WarmUpAuctionAsync(
            It.Is<AuctionEntity>(a => a.Id == auction.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        // DB đồng bộ sang ACTIVE và ví người chơi được đóng băng
        var refreshedAuction = await context.Auctions.FindAsync(auction.Id);
        refreshedAuction!.AuctionStatus.Should().Be(AuctionStatus.ACTIVE);
        wallet.WalletStatus.Should().Be(WalletStatus.FROZEN);

        // Hủy job START thừa và lập lịch END
        mockDelayQueue.Verify(q => q.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_PRE_WARM, It.IsAny<CancellationToken>()), Times.Once);
        mockDelayQueue.Verify(q => q.CancelJobAsync(auction.Id, AuctionDelayJobTypes.AUCTION_START, It.IsAny<CancellationToken>()), Times.Once);
        mockDelayQueue.Verify(q => q.EnqueueJobAsync(
            It.Is<AuctionDelayJob>(j => j.AuctionId == auction.Id && j.JobType == AuctionDelayJobTypes.AUCTION_END),
            It.IsAny<CancellationToken>()), Times.Once);

        // Phát SignalR ACTIVE và NewBid
        mockNotifier.Verify(n => n.NotifyAuctionStatusChangedAsync(
            auction.Id, AuctionStatus.ACTIVE.ToString(), null, It.IsAny<decimal?>(), It.IsAny<CancellationToken>()), Times.Once);
        mockNotifier.Verify(n => n.NotifyNewBidAsync(
            auction.Id, It.IsAny<Application.Features.Auction.DTOs.Responses.BidDto>(), 105_000_000m, 2, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PlaceBidCommandHandler_WhenBeforeStartAtAndCacheMiss_ShouldThrowNotActiveException()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (auction, participant, _) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddMinutes(5),
            DateTime.UtcNow.AddMinutes(35));

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Auctions).Returns(new AuctionRepository(context));

        var mockRedis = new Mock<IRedisAuctionCacheService>();
        mockRedis.Setup(r => r.PlaceBidAtomicAsync(
                auction.Id, participant.Id, participant.UserAccountId, 105_000_000m, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LuaBidResult(
                false, "ERR_AUCTION_CACHE_MISS", "Missing state",
                Guid.NewGuid(), auction.Id, participant.Id, participant.UserAccountId, 105_000_000m, 0, 0, false, 0));

        var handler = new PlaceBidCommandHandler(
            mockUow.Object,
            mockRedis.Object,
            Mock.Of<IAuctionRealtimeNotifier>(),
            Mock.Of<IAuctionDelayQueueService>());

        var act = async () => await handler.Handle(
            new PlaceBidCommand(105_000_000m, auction.Id, participant.UserAccountId),
            CancellationToken.None);

        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*chưa mở hoặc đã kết thúc*");
    }

    [Fact]
    public async Task AuctionPreWarmAndStartJobHandler_ShouldPreWarmWithoutFreezingThenActivateFastAtStartAt()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (auction, _, wallet) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddSeconds(5),
            DateTime.UtcNow.AddMinutes(30));

        var mockNotifier = new Mock<IAuctionRealtimeNotifier>();
        var mockLedger = new Mock<ILedgerService>();
        var mockDelayQueue = new Mock<IAuctionDelayQueueService>();
        var mockRedis = new Mock<IRedisAuctionCacheService>();
        var depositHelper = new SellerListingDepositHelper(NullLogger<SellerListingDepositHelper>.Instance);

        mockRedis.Setup(r => r.ActivateAuctionInCacheAsync(auction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var jobHandler = new AuctionPreWarmAndStartJobHandler(
            context,
            mockNotifier.Object,
            mockLedger.Object,
            mockDelayQueue.Object,
            depositHelper,
            NullLogger<AuctionPreWarmAndStartJobHandler>.Instance,
            mockRedis.Object);

        // Pha 1: T - 5s Pre-Warm -> WarmUp Redis với trạng thái SCHEDULE, chưa freeze ví
        await jobHandler.HandleAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.AUCTION_PRE_WARM,
            ExecuteAt = DateTime.UtcNow
        }, CancellationToken.None);

        mockRedis.Verify(r => r.WarmUpAuctionAsync(
            It.Is<AuctionEntity>(a => a.Id == auction.Id && a.AuctionStatus == AuctionStatus.SCHEDULE),
            It.IsAny<CancellationToken>()), Times.Once);
        wallet.WalletStatus.Should().Be(WalletStatus.ACTIVE);

        // Pha 2: T = StartAt -> Bật nhanh Redis ACTIVE, cập nhật DB ACTIVE, đóng băng ví, phát SignalR ACTIVE
        await jobHandler.HandleAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.AUCTION_START,
            ExecuteAt = DateTime.UtcNow
        }, CancellationToken.None);

        var updatedAuction = await context.Auctions.FindAsync(auction.Id);
        updatedAuction!.AuctionStatus.Should().Be(AuctionStatus.ACTIVE);
        wallet.WalletStatus.Should().Be(WalletStatus.FROZEN);

        mockNotifier.Verify(n => n.NotifyAuctionStatusChangedAsync(
            auction.Id, AuctionStatus.ACTIVE.ToString(), null, auction.CurrentPrice, It.IsAny<CancellationToken>()), Times.Once);
        mockDelayQueue.Verify(q => q.EnqueueJobAsync(
            It.Is<AuctionDelayJob>(j => j.AuctionId == auction.Id && j.JobType == AuctionDelayJobTypes.AUCTION_END),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuctionDatabaseSyncWorker_ShouldSchedulePreWarmAndRecoverMissingActiveRedisCache()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (schedAuction, _, _) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddMinutes(10),
            DateTime.UtcNow.AddHours(1));
        var (activeAuction, _, _) = CreateAuctionFixture(
            context,
            AuctionStatus.ACTIVE,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(50));

        var services = new ServiceCollection();
        services.AddSingleton<TrustSimDbContext>(context);
        using var sp = services.BuildServiceProvider();

        var delayQueue = CreateInMemoryDelayQueueService();
        var mockRedis = new Mock<IRedisAuctionCacheService>();
        mockRedis.Setup(r => r.IsAuctionWarmedUpAsync(activeAuction.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var worker = new AuctionDatabaseSyncWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            delayQueue,
            NullLogger<AuctionDatabaseSyncWorker>.Instance,
            mockRedis.Object);

        await worker.SyncDatabaseAuctionsToDelayQueueAsync(isStartup: true, CancellationToken.None);

        // Phiên ACTIVE bị thiếu cache Redis đã được tự động nạp lại
        mockRedis.Verify(r => r.WarmUpAuctionAsync(
            It.Is<AuctionEntity>(a => a.Id == activeAuction.Id),
            It.IsAny<CancellationToken>()), Times.Once);

        // Cả 2 lane (Pre-Warm + Start của SCHEDULE và End của ACTIVE) đều đã nằm trong Live queue
        var earliestLive = await delayQueue.GetNextJobTimeAsync(AuctionDelayQueueLane.Live);
        earliestLive.Should().NotBeNull();
        earliestLive!.Value.Should().BeCloseTo(schedAuction.StartAt.AddSeconds(-5), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task GetAuctionByIdQueryHandler_ShouldIncludeRecentServerUtcNowForClockCalibration()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (auction, participant, _) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddMinutes(2),
            DateTime.UtcNow.AddMinutes(30));

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Auctions).Returns(new AuctionRepository(context));

        var mockUser = new Mock<ICurrentUserService>();
        mockUser.Setup(u => u.IsAuthenticated).Returns(true);
        mockUser.Setup(u => u.UserId).Returns(participant.UserAccountId);

        var queryHandler = new GetAuctionByIdQueryHandler(mockUow.Object, mockUser.Object);
        var before = DateTime.UtcNow;
        var dto = await queryHandler.Handle(new GetAuctionByIdQuery(auction.Id), CancellationToken.None);
        var after = DateTime.UtcNow;

        dto.ServerUtcNow.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
        dto.IsUserRegistered.Should().BeTrue();
    }

    [Fact]
    public async Task AuctionEndJobHandler_WhenFinalRedisBidNotYetFlushed_ShouldWaitBrieflyAndSettleWithFlushedBid()
    {
        var dbName = Guid.NewGuid().ToString("N");
        using var handlerContext = TestTrustSimDbContext.CreateInMemory(dbName);
        var (auction, participant, _) = CreateAuctionFixture(
            handlerContext,
            AuctionStatus.ACTIVE,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow);

        var finalBidId = Guid.NewGuid();
        var mockRedis = new Mock<IRedisAuctionCacheService>();
        mockRedis.Setup(r => r.GetTopBiddersAsync(auction.Id, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LeaderboardTopEntry>
            {
                new(participant.Id, finalBidId, 120_000_000m, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
            });

        var mockLedger = new Mock<ILedgerService>();
        mockLedger.Setup(l => l.PostEscrowHoldAsync(
                It.IsAny<Guid>(), 120_000_000m, 50_000_000m, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerJournal { Id = Guid.NewGuid(), JournalCode = "JRN-TEST" });

        var services = new ServiceCollection();
        using var sp = services.BuildServiceProvider();

        var jobHandler = new AuctionEndJobHandler(
            handlerContext,
            Mock.Of<IAuctionRealtimeNotifier>(),
            mockLedger.Object,
            Mock.Of<IAuctionDelayQueueService>(),
            new SellerListingDepositHelper(NullLogger<SellerListingDepositHelper>.Instance),
            sp.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<AuctionEndJobHandler>.Instance,
            mockRedis.Object);

        // Giả lập AuctionBidPersistenceWorker xả bid cuối xuống DB chậm hơn 150ms
        var flushTask = Task.Run(async () =>
        {
            await Task.Delay(150);
            using var writerContext = TestTrustSimDbContext.CreateInMemory(dbName);
            writerContext.Bids.Add(new Bid
            {
                Id = finalBidId,
                AuctionId = auction.Id,
                ParticipantId = participant.Id,
                BidAmount = 120_000_000m,
                PlacedAt = DateTime.UtcNow,
                Status = BidStatus.ACCEPTED
            });
            await writerContext.SaveChangesAsync();
        });

        await jobHandler.HandleAsync(new AuctionDelayJob
        {
            AuctionId = auction.Id,
            JobType = AuctionDelayJobTypes.AUCTION_END,
            ExecuteAt = DateTime.UtcNow
        }, CancellationToken.None);

        await flushTask;

        using var verifyContext = TestTrustSimDbContext.CreateInMemory(dbName);
        var savedResult = await verifyContext.AuctionResults.FirstOrDefaultAsync(r => r.AuctionId == auction.Id);
        savedResult.Should().NotBeNull();
        savedResult!.WinnerId.Should().Be(participant.Id);
        savedResult.FinalPrice.Should().Be(120_000_000m);
        savedResult.AuctionResultStatus.Should().Be(AuctionResultStatus.CONFIRMED);
    }

    [Fact]
    public async Task PlaceBidCommandHandler_WhenConcurrencyHitWhileDbStillSchedule_ShouldReloadFreshAndActivate()
    {
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (auction, participant, _) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddSeconds(-2),
            DateTime.UtcNow.AddMinutes(30));

        var repo = new AuctionRepository(context);
        var saveAttempts = 0;

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Auctions).Returns(repo);
        mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct =>
            {
                saveAttempts++;
                if (saveAttempts == 1)
                {
                    throw new DbUpdateConcurrencyException("Simulated PersistenceWorker LockVersion bump");
                }

                return context.SaveChangesAsync(ct);
            });

        var mockRedis = new Mock<IRedisAuctionCacheService>();
        mockRedis.Setup(r => r.PlaceBidAtomicAsync(
                auction.Id, participant.Id, participant.UserAccountId, 105_000_000m, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid aId, Guid pId, Guid uId, decimal amt, Guid bId, CancellationToken _) => new LuaBidResult(
                true, null, null,
                bId, aId, pId, uId, amt, amt,
                new DateTimeOffset(auction.EndAt).ToUnixTimeMilliseconds(),
                false, 2, JustActivated: true));

        var mockNotifier = new Mock<IAuctionRealtimeNotifier>();
        var handler = new PlaceBidCommandHandler(
            mockUow.Object,
            mockRedis.Object,
            mockNotifier.Object,
            Mock.Of<IAuctionDelayQueueService>());

        var result = await handler.Handle(
            new PlaceBidCommand(105_000_000m, auction.Id, participant.UserAccountId),
            CancellationToken.None);

        result.BidAmount.Should().Be(105_000_000m);
        saveAttempts.Should().Be(2);

        var saved = await repo.GetFreshByIdAsync(auction.Id);
        saved!.AuctionStatus.Should().Be(AuctionStatus.ACTIVE);
        mockNotifier.Verify(n => n.NotifyAuctionStatusChangedAsync(
            auction.Id, AuctionStatus.ACTIVE.ToString(), null, It.IsAny<decimal?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DelayQueueAndSyncWorker_ShouldPreserveAntiSnipingEndAtAndSkipDuplicatePreWarm()
    {
        var delayQueue = CreateInMemoryDelayQueueService();
        var auctionId = Guid.NewGuid();
        var extendedEnd = DateTime.UtcNow.AddMinutes(10);
        var olderEnd = DateTime.UtcNow.AddMinutes(2);

        await delayQueue.EnqueueJobAsync(new AuctionDelayJob
        {
            AuctionId = auctionId,
            JobType = AuctionDelayJobTypes.AUCTION_END,
            ExecuteAt = extendedEnd
        });

        await delayQueue.EnqueueJobPreservingLaterAsync(new AuctionDelayJob
        {
            AuctionId = auctionId,
            JobType = AuctionDelayJobTypes.AUCTION_END,
            ExecuteAt = olderEnd
        }, CancellationToken.None);

        var nextEnd = await delayQueue.GetNextJobTimeAsync(AuctionDelayQueueLane.Live);
        nextEnd.Should().NotBeNull();
        nextEnd!.Value.Should().BeCloseTo(extendedEnd, TimeSpan.FromSeconds(1));

        // Kiểm tra AuctionDatabaseSyncWorker bỏ qua AUCTION_PRE_WARM khi chỉ còn 3s tới StartAt và Redis đã warm-up sẵn
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (schedAuction, _, _) = CreateAuctionFixture(
            context,
            AuctionStatus.SCHEDULE,
            DateTime.UtcNow.AddSeconds(3),
            DateTime.UtcNow.AddMinutes(30));

        var services = new ServiceCollection();
        services.AddSingleton<TrustSimDbContext>(context);
        using var sp = services.BuildServiceProvider();

        var freshQueue = CreateInMemoryDelayQueueService();
        var mockRedis = new Mock<IRedisAuctionCacheService>();
        mockRedis.Setup(r => r.IsAuctionWarmedUpAsync(schedAuction.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var worker = new AuctionDatabaseSyncWorker(
            sp.GetRequiredService<IServiceScopeFactory>(),
            freshQueue,
            NullLogger<AuctionDatabaseSyncWorker>.Instance,
            mockRedis.Object);

        await worker.SyncDatabaseAuctionsToDelayQueueAsync(isStartup: false, CancellationToken.None);

        var earliestLive = await freshQueue.GetNextJobTimeAsync(AuctionDelayQueueLane.Live);
        earliestLive.Should().NotBeNull();
        earliestLive!.Value.Should().BeCloseTo(schedAuction.StartAt, TimeSpan.FromMilliseconds(500));
    }
}
