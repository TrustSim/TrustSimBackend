using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.Commands;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Infrastructure.Services.Financial;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Financial;

/// <summary>
/// Unit tests verifying wallet non-negative balance invariants, atomic ledger validation,
/// seller listing deposit safeguards, and DbSeeder ledger reconciliation.
/// </summary>
public class WalletInvariantAndSellerDepositTests
{
    private static (
        Mock<IUnitOfWork> MockUow,
        DoubleEntryLedgerService LedgerService,
        LedgerIntegrityVerifier IntegrityVerifier) CreateFinancialServices(TestTrustSimDbContext context)
    {
        var mockUow = new Mock<IUnitOfWork>(MockBehavior.Strict);
        mockUow.Setup(u => u.Wallets).Returns(new WalletRepository(context));
        mockUow.Setup(u => u.LedgerJournals).Returns(new LedgerJournalRepository(context));
        mockUow.Setup(u => u.LedgerEntries).Returns(new LedgerEntryRepository(context));
        mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(ct => context.SaveChangesAsync(ct));

        var signingService = new EcdsaFinancialSigningService(NullLogger<EcdsaFinancialSigningService>.Instance);
        var ledgerService = new DoubleEntryLedgerService(
            mockUow.Object,
            signingService,
            NullLogger<DoubleEntryLedgerService>.Instance);
        var integrityVerifier = new LedgerIntegrityVerifier(
            mockUow.Object,
            signingService,
            NullLogger<LedgerIntegrityVerifier>.Instance);

        return (mockUow, ledgerService, integrityVerifier);
    }

    private static async Task<Wallet> SeedUserWithWalletAsync(
        TestTrustSimDbContext context,
        decimal availableBalance,
        decimal lockedBalance)
    {
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            UserName = $"tester_{userId:N}",
            Email = $"tester_{userId:N}@trustsim.vn",
            IsActive = true,
            IsDeleted = false
        };

        var wallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = userId,
            AvailableBalance = availableBalance,
            LockedBalance = lockedBalance,
            WalletStatus = WalletStatus.ACTIVE
        };

        await context.UserAccounts.AddAsync(user);
        await context.Wallets.AddAsync(wallet);
        await context.SaveChangesAsync();
        return wallet;
    }

    [Fact]
    public async Task PostSellerListingDepositReleaseAsync_WhenLockedBalanceInsufficient_ShouldThrowAndNotMutateWalletOrLedger()
    {
        // Arrange
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (_, ledgerService, _) = CreateFinancialServices(context);
        var wallet = await SeedUserWithWalletAsync(context, 995_000_000m, 5_000_000m);

        // Act
        Func<Task> act = async () => await ledgerService.PostSellerListingDepositReleaseAsync(
            wallet.Id,
            25_000_000m,
            Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*VI PHẠM BẤT BIẾN TÀI CHÍNH*Số dư phong tỏa*");

        context.ChangeTracker.Clear();
        var persistedWallet = await context.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id);
        persistedWallet.AvailableBalance.Should().Be(995_000_000m);
        persistedWallet.LockedBalance.Should().Be(5_000_000m);
        (await context.LedgerJournals.CountAsync()).Should().Be(0);
        (await context.LedgerEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PostJournalAsync_WhenUserAvailableBalanceInsufficient_ShouldThrowAtomicallyWithoutLedgerMutation()
    {
        // Arrange
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (_, ledgerService, _) = CreateFinancialServices(context);
        var wallet = await SeedUserWithWalletAsync(context, 1_000_000m, 0m);

        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.DEBIT, 2_000_000m, wallet.Id, "Lock exceeds available"),
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.CREDIT, 2_000_000m, wallet.Id, "Lock exceeds available")
        };

        // Act
        Func<Task> act = async () => await ledgerService.PostJournalAsync(
            LedgerTransactionType.AUCTION_DEPOSIT_LOCK,
            "Insufficient available balance test",
            entries,
            referenceType: "Test",
            referenceId: Guid.NewGuid(),
            idempotencyKey: $"TEST-INSUFF-{Guid.NewGuid():N}");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*VI PHẠM BẤT BIẾN TÀI CHÍNH*Số dư khả dụng*");

        context.ChangeTracker.Clear();
        var persistedWallet = await context.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id);
        persistedWallet.AvailableBalance.Should().Be(1_000_000m);
        persistedWallet.LockedBalance.Should().Be(0m);
        (await context.LedgerJournals.CountAsync()).Should().Be(0);
        (await context.LedgerEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PostSellerListingDepositLockAndRelease_WhenSufficientBalance_ShouldUpdateBalancesAndPassIntegrityAudit()
    {
        // Arrange
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (_, ledgerService, integrityVerifier) = CreateFinancialServices(context);
        var wallet = await SeedUserWithWalletAsync(context, 0m, 0m);

        var auctionId = Guid.NewGuid();
        const decimal initialDeposit = 100_000_000m;
        const decimal sellerListingDeposit = 25_000_000m;

        // Act
        await ledgerService.PostDepositAsync(wallet.Id, initialDeposit, "TOPUP-TEST-001", "EXT-001");
        await context.SaveChangesAsync();

        await ledgerService.PostSellerListingDepositLockAsync(wallet.Id, sellerListingDeposit, auctionId);
        await context.SaveChangesAsync();

        var lockedWallet = await context.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id);
        lockedWallet.AvailableBalance.Should().Be(75_000_000m);
        lockedWallet.LockedBalance.Should().Be(25_000_000m);

        await ledgerService.PostSellerListingDepositReleaseAsync(wallet.Id, sellerListingDeposit, auctionId);
        await context.SaveChangesAsync();

        // Assert
        var releasedWallet = await context.Wallets.AsNoTracking().SingleAsync(w => w.Id == wallet.Id);
        releasedWallet.AvailableBalance.Should().Be(100_000_000m);
        releasedWallet.LockedBalance.Should().Be(0m);

        var auditReport = await integrityVerifier.GenerateFullAuditReportAsync();
        auditReport.IsChainValid.Should().BeTrue();
        auditReport.IsSignatureValid.Should().BeTrue();
        auditReport.IsBalanceReconciled.Should().BeTrue();
        auditReport.IsAccountingEquationBalanced.Should().BeTrue();
        auditReport.HasFraudDetected.Should().BeFalse();
    }

    [Fact]
    public async Task AdminCancelAuction_WhenSellerLockedBalanceInsufficient_ShouldNotReleaseSellerDeposit()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var simNumberId = Guid.NewGuid();

        var simNumber = new SimNumber
        {
            Id = simNumberId,
            PhoneNumber = "0988123456",
            SimStatus = SimStatus.IN_AUCTION
        };

        var auction = new Auction
        {
            Id = auctionId,
            AuctionCode = "AUC-CANCEL-TEST-001",
            AuctionStatus = AuctionStatus.ACTIVE,
            AuctionType = AuctionType.ENGLISH,
            StartingPrice = 500_000_000m,
            CurrentPrice = 500_000_000m,
            AuthorId = sellerId,
            SimNumberId = simNumberId,
            SimNumber = simNumber,
            AuctionRule = new AuctionRule
            {
                Id = Guid.NewGuid(),
                AuctionId = auctionId,
                SellerDepositAmount = 25_000_000m
            },
            Participants = new List<AuctionParticipant>()
        };

        var sellerWallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = sellerId,
            AvailableBalance = 995_000_000m,
            LockedBalance = 5_000_000m,
            WalletStatus = WalletStatus.ACTIVE
        };

        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        mockUow.Setup(u => u.Auctions.GetDetailByIdAsync(auctionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(auction);
        mockUow.Setup(u => u.SimNumbers.GetByIdAsync(simNumberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(simNumber);
        mockUow.Setup(u => u.Wallets.GetByUserIdAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerWallet);
        mockUow.Setup(u => u.Auctions.GetResultByAuctionIdAsync(auctionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuctionResult?)null);

        var mockLedgerService = new Mock<ILedgerService>();
        var mockRealtimeNotifier = new Mock<IAuctionRealtimeNotifier>();
        var mockDelayQueue = new Mock<IAuctionDelayQueueService>();
        var mockNotificationService = new Mock<INotificationService>();

        var handler = new AdminCancelAuctionCommandHandler(
            mockUow.Object,
            mockLedgerService.Object,
            mockRealtimeNotifier.Object,
            mockDelayQueue.Object,
            mockNotificationService.Object);

        var command = new AdminCancelAuctionCommand(auctionId, "Admin test cancellation", adminId);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        auction.AuctionStatus.Should().Be(AuctionStatus.CANCELLED);
        simNumber.SimStatus.Should().Be(SimStatus.AVAILABLE);
        mockLedgerService.Verify(
            l => l.PostSellerListingDepositReleaseAsync(
                It.IsAny<Guid>(),
                It.IsAny<decimal>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DbSeeder_SeedAsync_ShouldInitializeSellerListingDepositsAndPassLedgerIntegrityAuditIdempotently()
    {
        // Arrange
        using var context = TestTrustSimDbContext.CreateInMemory();
        var (_, _, integrityVerifier) = CreateFinancialServices(context);

        // Act
        await DbSeeder.SeedAsync(context);
        await DbSeeder.SeedAsync(context);

        // Assert: Zero negative wallet balances across all seeded wallets
        var allWallets = await context.Wallets.AsNoTracking().ToListAsync();
        allWallets.Should().NotBeEmpty();
        allWallets.Should().OnlyContain(w => w.AvailableBalance >= 0m && w.LockedBalance >= 0m);

        // Verify seller_vip_phuquy wallet reflects 1B topup minus 105M locked (auc1=25M, auc3=75M, auc6=5M)
        var phuQuyUser = await context.UserAccounts.AsNoTracking()
            .SingleAsync(u => u.UserName == "seller_vip_phuquy");
        var phuQuyWallet = allWallets.Single(w => w.UserAccountId == phuQuyUser.Id);
        phuQuyWallet.AvailableBalance.Should().Be(895_000_000m);
        phuQuyWallet.LockedBalance.Should().Be(105_000_000m);

        // Verify seller_daiphat wallet reflects 1B topup minus 9M locked (auc2=5M, auc4=4M)
        var daiPhatUser = await context.UserAccounts.AsNoTracking()
            .SingleAsync(u => u.UserName == "seller_daiphat");
        var daiPhatWallet = allWallets.Single(w => w.UserAccountId == daiPhatUser.Id);
        daiPhatWallet.AvailableBalance.Should().Be(991_000_000m);
        daiPhatWallet.LockedBalance.Should().Be(9_000_000m);

        // Verify SELLER_LISTING_DEPOSIT_LOCK ledger journals exist for all 5 seeded auctions with deposit > 0
        var seededAuctionIds = await context.AuctionRules.AsNoTracking()
            .Where(r => r.SellerDepositAmount > 0m)
            .Select(r => r.AuctionId)
            .ToListAsync();
        seededAuctionIds.Should().HaveCount(5);

        var lockKeys = await context.LedgerJournals.AsNoTracking()
            .Where(j => j.TransactionType == LedgerTransactionType.SELLER_LISTING_DEPOSIT_LOCK)
            .Select(j => j.IdempotencyKey)
            .ToListAsync();
        foreach (var auctionId in seededAuctionIds)
        {
            lockKeys.Should().Contain($"SELLER-LOCK-{auctionId}");
        }

        // Verify full cryptographic ledger & balance reconciliation audit report
        var auditReport = await integrityVerifier.GenerateFullAuditReportAsync();
        auditReport.IsChainValid.Should().BeTrue();
        auditReport.IsSignatureValid.Should().BeTrue();
        auditReport.IsBalanceReconciled.Should().BeTrue();
        auditReport.IsAccountingEquationBalanced.Should().BeTrue();
        auditReport.HasFraudDetected.Should().BeFalse();
    }
}
