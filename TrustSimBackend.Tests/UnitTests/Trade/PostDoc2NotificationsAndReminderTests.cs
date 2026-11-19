using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Hubs;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Dispute.Commands;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Auctions;
using AuctionEntity = TrustSimBackend.Domain.Entities.Auctions.Auction;
using TrustSimBackend.Domain.Entities.Dispute;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence;
using TrustSimBackend.Infrastructure.Services;
using TrustSimBackend.Infrastructure.Workers.Auction.DelayJobs;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Trade;

/// <summary>
/// Unit tests verifying post-Document #2 notification routing, roles dispatch, URL safety,
/// dispute/admin trade status events, timeout/liquidation jobs, and deduplicated periodic reminders.
/// </summary>
public class PostDoc2NotificationsAndReminderTests
{
    private static readonly UserRole[] StaffRoles = { UserRole.ADMIN, UserRole.CONSULTANT };

    private static IFormFile CreateFakeFormFile(string fileName = "proof.jpg", int size = 512)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(size);
        return mockFile.Object;
    }

    private static TradeTransaction BuildBaseTrade(
        out Guid tradeId,
        out Guid auctionId,
        out Guid sellerId,
        out Guid buyerId,
        out Guid simNumberId)
    {
        tradeId = Guid.NewGuid();
        auctionId = Guid.NewGuid();
        sellerId = Guid.NewGuid();
        buyerId = Guid.NewGuid();
        simNumberId = Guid.NewGuid();

        var simNumber = new SimNumber { Id = simNumberId, PhoneNumber = "0988999888" };
        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
        };

        return new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            SellerId = sellerId,
            BuyerId = buyerId,
            SimNumberId = simNumberId,
            SimNumber = simNumber,
            TradeTransactionCode = "TRD-NOTIF-001",
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
            TransferDeadline = DateTime.UtcNow.AddHours(24),
            SimTransfer = simTransfer
        };
    }

    private static TransferProof CreateProof(
        Guid auctionId,
        Guid simTransferId,
        Guid submitterId,
        TransferProofType type,
        ProofStatus status)
    {
        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            SimTransferId = simTransferId,
            SubmittedByUserId = submitterId,
            ProofType = type,
            ProofStatus = status,
            CreatedDate = DateTime.UtcNow
        };
        proof.AddImage(new TransferProofImage
        {
            Id = Guid.NewGuid(),
            TransferProofId = proof.Id,
            ImageUrl = "https://cdn.trustsim.vn/proof.jpg",
            FileName = "proof.jpg",
            ContentType = "image/jpeg",
            SortOrder = 0
        });
        return proof;
    }

    [Theory]
    [InlineData(TransferProofType.SELLER_HANDOVER, true)]
    [InlineData(TransferProofType.BUYER_RECEIPT, false)]
    public async Task SubmitMultipleTransferProofs_ShouldNotifyCounterpartyAndStaffWithSendEmailFalse(
        TransferProofType proofType,
        bool submitterIsSeller)
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockStorage = new Mock<ICloudStorageService>();
        var mockNotif = new Mock<INotificationService>();
        var mockDelay = new Mock<IAuctionDelayQueueService>();

        var trade = BuildBaseTrade(out var tradeId, out _, out var sellerId, out var buyerId, out _);
        var submitterId = submitterIsSeller ? sellerId : buyerId;
        var counterpartyId = submitterIsSeller ? buyerId : sellerId;

        if (!submitterIsSeller)
        {
            trade.SimTransfer!.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;
            trade.SimTransfer.AddTransferProof(CreateProof(
                trade.AuctionId, trade.SimTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.APPROVED));
        }

        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        mockStorage.Setup(s => s.UploadImageAsync(It.IsAny<IFormFile>(), "sim-transfer", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.trustsim.vn/sim-transfer/img1.jpg");

        var handler = new SubmitMultipleTransferProofsCommandHandler(
            mockUow.Object,
            mockStorage.Object,
            mockNotif.Object,
            mockDelay.Object,
            Options.Create(new TradeDeadlineSettings { LiquidationSigningDeadlineHours = 72 }),
            NullLogger<SubmitMultipleTransferProofsCommandHandler>.Instance);

        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: submitterId,
            Files: new List<IFormFile> { CreateFakeFormFile() },
            ProofType: proofType,
            Description: "Upload transfer proof");

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        mockNotif.Verify(n => n.SendNotificationAsync(
            counterpartyId,
            NotificationType.TRANSFER_PROOF_SUBMITTED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/trade/{tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationToRolesAsync(
            It.Is<IReadOnlyCollection<UserRole>>(r => r.Contains(UserRole.ADMIN) && r.Contains(UserRole.CONSULTANT)),
            NotificationType.TRANSFER_PROOF_SUBMITTED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/transfer-review?tradeId={tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            false,
            submitterId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WithdrawTransferProof_WhenSellerWithdraws_ShouldNotifyBuyerAndStaffWithTransferProofWithdrawn()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockDelay = new Mock<IAuctionDelayQueueService>();
        var mockNotif = new Mock<INotificationService>();

        var trade = BuildBaseTrade(out var tradeId, out var auctionId, out var sellerId, out var buyerId, out _);
        trade.SimTransfer!.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;
        var pendingProof = CreateProof(auctionId, trade.SimTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING);
        trade.SimTransfer.AddTransferProof(pendingProof);

        mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(pendingProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingProof);
        mockUow.Setup(u => u.TradeTransactions.GetByAuctionIdAsync(auctionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new WithdrawTransferProofCommandHandler(
            mockUow.Object,
            mockDelay.Object,
            NullLogger<WithdrawTransferProofCommandHandler>.Instance,
            mockNotif.Object);

        // Act
        await handler.Handle(new WithdrawTransferProofCommand(pendingProof.Id, sellerId), CancellationToken.None);

        // Assert
        mockNotif.Verify(n => n.SendNotificationAsync(
            buyerId,
            NotificationType.TRANSFER_PROOF_WITHDRAWN,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/trade/{tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationToRolesAsync(
            It.Is<IReadOnlyCollection<UserRole>>(r => r.Contains(UserRole.ADMIN) && r.Contains(UserRole.CONSULTANT)),
            NotificationType.TRANSFER_PROOF_WITHDRAWN,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/transfer-review?tradeId={tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            false,
            sellerId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewSimTransferProof_SellerApproved_ShouldNotifyBuyerWithEmailAndSellerInApp()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockNotif = new Mock<INotificationService>();
        var trade = BuildBaseTrade(out var tradeId, out var auctionId, out var sellerId, out var buyerId, out _);
        trade.SimTransfer!.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;
        var sellerProof = CreateProof(auctionId, trade.SimTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING);
        trade.SimTransfer.AddTransferProof(sellerProof);

        mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(sellerProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerProof);
        mockUow.Setup(u => u.TradeTransactions.GetByAuctionIdAsync(auctionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new ReviewSimTransferProofCommandHandler(
            mockUow.Object,
            new Mock<ILedgerService>().Object,
            mockNotif.Object,
            new Mock<IPdfContractService>().Object,
            new Mock<IPdfSigningService>().Object,
            new Mock<ICloudStorageService>().Object,
            new Mock<IAuctionDelayQueueService>().Object,
            NullLogger<ReviewSimTransferProofCommandHandler>.Instance);

        // Act
        await handler.Handle(new ReviewSimTransferProofCommand(sellerProof.Id, Guid.NewGuid(), true, null), CancellationToken.None);

        // Assert
        mockNotif.Verify(n => n.SendNotificationAsync(
            buyerId,
            NotificationType.TRANSFER_PROOF_APPROVED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/trade/{tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.TRANSFER_PROOF_APPROVED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/trade/{tradeId}",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            false,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmSimTransfer_WhenBuyerConfirms_ShouldNotifyEscrowPayoutToSellerAndCompletionToBoth()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockLedger = new Mock<ILedgerService>();
        var mockNotif = new Mock<INotificationService>();
        var mockPdfContract = new Mock<IPdfContractService>();
        var mockPdfSign = new Mock<IPdfSigningService>();
        var mockCloud = new Mock<ICloudStorageService>();
        var mockDelay = new Mock<IAuctionDelayQueueService>();

        var trade = BuildBaseTrade(out var tradeId, out var auctionId, out var sellerId, out var buyerId, out var simNumberId);
        trade.SimTransfer!.SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION;
        trade.SimTransfer.AddTransferProof(CreateProof(auctionId, trade.SimTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.APPROVED));
        trade.Escrow = new Escrow { Id = Guid.NewGuid(), EscrowAmount = 20_000_000m, EscrowStatus = EscrowStatus.HELD };
        trade.TransferContract = new TransferContract { Id = Guid.NewGuid(), TradeTransactionId = tradeId, ContractStatus = ContractStatus.FULLY_SIGNED };

        var sellerWallet = new Wallet { Id = Guid.NewGuid(), UserAccountId = sellerId, AvailableBalance = 19_000_000m, WalletStatus = WalletStatus.ACTIVE };
        var pades = new PadesSignResult(new byte[] { 1, 2 }, "hash", "serial", DateTime.UtcNow, DateTime.UtcNow, "TSA");

        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>())).ReturnsAsync(trade);
        mockUow.Setup(u => u.Wallets.GetByUserIdAsync(sellerId, It.IsAny<CancellationToken>())).ReturnsAsync(sellerWallet);
        mockUow.Setup(u => u.SimOwnerships.GetActiveOwnershipsBySimNumberIdAsync(simNumberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SimOwnership>());
        mockPdfContract.Setup(p => p.GenerateLiquidationMinutePdfAsync(tradeId, It.IsAny<CancellationToken>())).ReturnsAsync(new byte[] { 1 });
        mockPdfContract.Setup(p => p.GetStampBoxesAsync(tradeId, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<SignatureStampBoxDto>());
        mockPdfSign.Setup(s => s.SignPdfPadesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<SignatureStampBoxDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(pades);
        mockCloud.Setup(c => c.UploadImageBytesAsync(It.IsAny<byte[]>(), It.IsAny<string>(), "contracts", "application/pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.trustsim.vn/contracts/liq_vb3.pdf");
        mockLedger.Setup(l => l.PostEscrowReleaseAsync(sellerWallet.Id, 20_000_000m, 1_000_000m, trade.Escrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerJournal { Id = Guid.NewGuid(), JournalCode = "JRN-REL-1" });

        var handler = new ConfirmSimTransferCommandHandler(
            mockUow.Object,
            mockLedger.Object,
            mockNotif.Object,
            mockPdfContract.Object,
            mockPdfSign.Object,
            mockCloud.Object,
            mockDelay.Object,
            NullLogger<ConfirmSimTransferCommandHandler>.Instance);

        // Act
        await handler.Handle(new ConfirmSimTransferCommand(tradeId, buyerId), CancellationToken.None);

        // Assert
        mockNotif.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.ESCROW_RELEASED_SELLER_PAYOUT,
            It.IsAny<string>(),
            It.Is<string>(c => c.Contains("19,000,000")),
            "https://cdn.trustsim.vn/contracts/liq_vb3.pdf",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationAsync(
            buyerId,
            NotificationType.TRADE_COMPLETED_LIQUIDATED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            "https://cdn.trustsim.vn/contracts/liq_vb3.pdf",
            "TradeTransaction",
            tradeId,
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.TRADE_COMPLETED_LIQUIDATED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateDispute_And_ResolveDispute_ShouldDispatchCorrectNotifications()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockNotif = new Mock<INotificationService>();
        var trade = BuildBaseTrade(out var tradeId, out _, out var sellerId, out var buyerId, out _);

        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var createHandler = new CreateDisputeCommandHandler(mockUow.Object, mockNotif.Object);
        var created = await createHandler.Handle(
            new CreateDisputeCommand(tradeId, "SIM mất sóng sau 2 giờ", buyerId),
            CancellationToken.None);

        // Assert CreateDispute
        mockNotif.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.DISPUTE_OPENED,
            It.IsAny<string>(),
            It.Is<string>(c => c.Contains("SIM mất sóng sau 2 giờ")),
            $"/disputes/{created.Id}",
            "DisputeTicket",
            created.Id,
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        mockNotif.Verify(n => n.SendNotificationToRolesAsync(
            It.Is<IReadOnlyCollection<UserRole>>(r => r.Contains(UserRole.ADMIN) && r.Contains(UserRole.CONSULTANT)),
            NotificationType.DISPUTE_OPENED,
            It.IsAny<string>(),
            It.IsAny<string>(),
            $"/disputes/{created.Id}",
            "DisputeTicket",
            created.Id,
            It.IsAny<object?>(),
            false,
            buyerId,
            It.IsAny<CancellationToken>()), Times.Once);

        // Arrange ResolveDispute
        var ticket = new DisputeTicket
        {
            Id = created.Id,
            TradeTransactionId = tradeId,
            ComplainantId = buyerId,
            Reason = "SIM mất sóng sau 2 giờ",
            Status = DisputeStatus.OPEN
        };
        mockUow.Setup(u => u.DisputeTickets.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveHandler = new ResolveDisputeCommandHandler(mockUow.Object, mockNotif.Object);

        // Act ResolveDispute
        var resolved = await resolveHandler.Handle(
            new ResolveDisputeCommand(DisputeStatus.RESOLVED_FAVOR_BUYER, "Hoàn tiền cho người mua", ticket.Id, Guid.NewGuid()),
            CancellationToken.None);

        // Assert ResolveDispute
        resolved.Should().BeTrue();
        foreach (var recipientId in new[] { buyerId, sellerId })
        {
            mockNotif.Verify(n => n.SendNotificationAsync(
                recipientId,
                NotificationType.DISPUTE_RESOLVED,
                It.IsAny<string>(),
                It.Is<string>(c => c.Contains("Hoàn tiền cho người mua")),
                $"/disputes/{ticket.Id}",
                "DisputeTicket",
                ticket.Id,
                It.IsAny<object?>(),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Theory]
    [InlineData(TradeTransactionStatus.Cancelled_ByAdmin, NotificationType.TRADE_CANCELLED_BY_ADMIN)]
    [InlineData(TradeTransactionStatus.Disputed, NotificationType.TRADE_STATUS_UPDATED)]
    public async Task UpdateTradeStatus_ShouldNotifyBuyerAndSellerWithMatchingTypeAndEmail(
        TradeTransactionStatus newStatus,
        NotificationType expectedType)
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockNotif = new Mock<INotificationService>();
        var trade = BuildBaseTrade(out var tradeId, out _, out var sellerId, out var buyerId, out _);

        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new UpdateTradeStatusCommandHandler(mockUow.Object, mockNotif.Object);

        // Act
        var ok = await handler.Handle(new UpdateTradeStatusCommand(tradeId, newStatus), CancellationToken.None);

        // Assert
        ok.Should().BeTrue();
        foreach (var recipientId in new[] { buyerId, sellerId })
        {
            mockNotif.Verify(n => n.SendNotificationAsync(
                recipientId,
                expectedType,
                It.IsAny<string>(),
                It.IsAny<string>(),
                $"/trade/{tradeId}",
                "TradeTransaction",
                tradeId,
                It.IsAny<object?>(),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task TradeDeadlineJobHandler_TransferTimeout_ShouldNotifyTradeCancelledTimeout()
    {
        // Arrange
        await using var context = TestTrustSimDbContext.CreateInMemory();
        var mockLedger = new Mock<ILedgerService>();
        var mockNotif = new Mock<INotificationService>();

        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();
        var simNumberId = Guid.NewGuid();

        var seller = new UserAccount { Id = sellerId, UserName = "seller_to", Email = "s@test.vn" };
        var buyer = new UserAccount { Id = buyerId, UserName = "buyer_to", Email = "b@test.vn" };
        var buyerWallet = new Wallet { Id = Guid.NewGuid(), UserAccountId = buyerId, AvailableBalance = 10_000_000m, WalletStatus = WalletStatus.ACTIVE };
        var sim = new SimNumber { Id = simNumberId, PhoneNumber = "0977555444", SimStatus = SimStatus.IN_AUCTION };
        var auction = new AuctionEntity
        {
            Id = auctionId,
            AuctionCode = "AUC-TO-1",
            AuthorId = sellerId,
            SimNumberId = simNumberId,
            AuctionStatus = AuctionStatus.END
        };
        var escrow = new Escrow
        {
            Id = Guid.NewGuid(),
            EscrowAmount = 5_000_000m,
            EscrowStatus = EscrowStatus.HELD
        };
        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            SellerId = sellerId,
            BuyerId = buyerId,
            SimNumberId = simNumberId,
            Escrow = escrow,
            TradeTransactionCode = "TRD-TO-1",
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer
        };
        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
        };

        mockLedger.Setup(l => l.PostEscrowRefundAsync(buyerWallet.Id, 5_000_000m, escrow.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerJournal { Id = Guid.NewGuid(), JournalCode = "JRN-REF-1" });

        await context.UserAccounts.AddRangeAsync(seller, buyer);
        await context.Wallets.AddAsync(buyerWallet);
        await context.SimNumbers.AddAsync(sim);
        await context.Auctions.AddAsync(auction);
        await context.Escrows.AddAsync(escrow);
        await context.TradeTransactions.AddAsync(trade);
        await context.SimTransfers.AddAsync(simTransfer);
        await context.SaveChangesAsync();

        var jobHandler = new TradeDeadlineJobHandler(
            context,
            mockLedger.Object,
            new SellerListingDepositHelper(NullLogger<SellerListingDepositHelper>.Instance),
            NullLogger<TradeDeadlineJobHandler>.Instance,
            notificationService: mockNotif.Object);

        // Act
        await jobHandler.HandleAsync(new AuctionDelayJob
        {
            AuctionId = auctionId,
            JobType = AuctionDelayJobTypes.TRANSFER_DEADLINE,
            ExtraData = tradeId.ToString()
        }, CancellationToken.None);

        // Assert
        foreach (var recipientId in new[] { buyerId, sellerId })
        {
            mockNotif.Verify(n => n.SendNotificationAsync(
                recipientId,
                NotificationType.TRADE_CANCELLED_TIMEOUT,
                It.IsAny<string>(),
                It.IsAny<string>(),
                $"/trade/{tradeId}",
                "TradeTransaction",
                tradeId,
                It.IsAny<object?>(),
                true,
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    [Fact]
    public async Task NotificationService_ShouldPreserveOperationalUrlsForStaffAndRejectedPartyAndStripOutsiders()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var consultantId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = Guid.NewGuid(),
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-NS-1",
            TransferContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = tradeId,
                WinningNoticeUrl = "https://demo.supabase.co/storage/vb1.pdf",
                DocumentUrl = "https://demo.supabase.co/storage/vb2.pdf"
            }
        };

        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        mockUow.Setup(u => u.Users.GetByIdAsync(adminId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount { Id = adminId, Role = UserRole.ADMIN });
        mockUow.Setup(u => u.Users.GetByIdAsync(outsiderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserAccount { Id = outsiderId, Role = UserRole.USER });
        mockUow.Setup(u => u.Users.GetPagedAsync(It.Is<AdminUsersQueryFilter>(f => f.Role == UserRole.ADMIN), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserAccount> { new() { Id = adminId }, new() { Id = sellerId } }, 2));
        mockUow.Setup(u => u.Users.GetPagedAsync(It.Is<AdminUsersQueryFilter>(f => f.Role == UserRole.CONSULTANT), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new List<UserAccount> { new() { Id = consultantId }, new() { Id = adminId } }, 2));

        var services = new ServiceCollection();
        services.AddSingleton(mockUow.Object);
        services.AddSingleton(new Mock<IEmailService>().Object);
        var provider = services.BuildServiceProvider();

        var service = new NotificationService(
            mockUow.Object,
            new Mock<IEmailService>().Object,
            new Mock<IAuctionRealtimeNotifier>().Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<NotificationService>.Instance);

        // Act
        var rejectedNotif = await service.SendNotificationAsync(
            sellerId, NotificationType.TRANSFER_PROOF_REJECTED, "Rejected", "Reason",
            $"/trade/{tradeId}", "TradeTransaction", tradeId);
        var contractNotif = await service.SendNotificationAsync(
            buyerId, NotificationType.CONTRACT_FULLY_SIGNED, "Signed", "VB2",
            $"/trade/{tradeId}", "TradeTransaction", tradeId);
        var adminNotif = await service.SendNotificationAsync(
            adminId, NotificationType.TRANSFER_PROOF_SUBMITTED, "Submitted", "Check",
            $"/transfer-review?tradeId={tradeId}", "TradeTransaction", tradeId);
        var outsiderNotif = await service.SendNotificationAsync(
            outsiderId, NotificationType.TRANSFER_PROOF_REJECTED, "Rejected", "Reason",
            $"/trade/{tradeId}", "TradeTransaction", tradeId);

        var staffCount = await service.SendNotificationToRolesAsync(
            StaffRoles, NotificationType.TRANSFER_PROOF_SUBMITTED, "Review", "New proof",
            $"/transfer-review?tradeId={tradeId}", "TradeTransaction", tradeId,
            sendEmail: false, excludeUserId: sellerId);

        // Assert
        rejectedNotif.ActionUrl.Should().Be($"/trade/{tradeId}");
        contractNotif.ActionUrl.Should().Be("https://demo.supabase.co/storage/vb2.pdf");
        adminNotif.ActionUrl.Should().Be($"/transfer-review?tradeId={tradeId}");
        outsiderNotif.ActionUrl.Should().BeNull();
        staffCount.Should().Be(2); // Deduplicated adminId + consultantId; excluded sellerId
    }

    [Fact]
    public async Task NotificationService_WhenStringsAndMetadataExceedDbLimits_ShouldTruncateWithinLimitsAndKeepValidJson()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var services = new ServiceCollection();
        services.AddSingleton(mockUow.Object);
        services.AddSingleton(new Mock<IEmailService>().Object);
        var provider = services.BuildServiceProvider();

        var service = new NotificationService(
            mockUow.Object,
            new Mock<IEmailService>().Object,
            new Mock<IAuctionRealtimeNotifier>().Object,
            provider.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),
            NullLogger<NotificationService>.Instance);

        var tradeId = Guid.NewGuid();
        var longTitle = new string('T', 350);
        var longContent = new string('C', 2200);
        var longUrl = "/trade/" + new string('u', 650);
        var largeMeta = new
        {
            tradeId,
            tradeCode = "TRD-BIG-001",
            reason = new string('R', 5000),
            stage = "DISPUTE_OPENED"
        };

        // Act
        var saved = await service.SendNotificationAsync(
            Guid.NewGuid(),
            NotificationType.DISPUTE_OPENED,
            longTitle,
            longContent,
            actionUrl: longUrl,
            referenceType: new string('X', 80),
            referenceId: tradeId,
            metadata: largeMeta);

        // Assert
        saved.Title.Length.Should().BeLessThanOrEqualTo(200);
        saved.Title.Should().EndWith("...");
        saved.Content.Length.Should().BeLessThanOrEqualTo(1000);
        saved.Content.Should().EndWith("...");
        saved.ActionUrl!.Length.Should().BeLessThanOrEqualTo(500);
        saved.ReferenceType!.Length.Should().BeLessThanOrEqualTo(50);
        saved.MetadataJson!.Length.Should().BeLessThanOrEqualTo(4000);

        using var parsed = JsonDocument.Parse(saved.MetadataJson);
        parsed.RootElement.GetProperty("tradeCode").GetString().Should().Be("TRD-BIG-001");
        parsed.RootElement.GetProperty("stage").GetString().Should().Be("DISPUTE_OPENED");
    }

    [Fact]
    public async Task CreateDispute_And_ResolveDispute_WhenNotificationFailsAfterCommit_ShouldStillSucceed()
    {
        // Arrange
        var mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        var mockNotif = new Mock<INotificationService>();
        var trade = BuildBaseTrade(out var tradeId, out _, out var sellerId, out var buyerId, out _);

        mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        mockNotif.Setup(n => n.SendNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Transient notification failure"));

        var createHandler = new CreateDisputeCommandHandler(mockUow.Object, mockNotif.Object);
        var created = await createHandler.Handle(new CreateDisputeCommand(tradeId, "Sim mất sóng đột ngột", buyerId), CancellationToken.None);

        var ticket = new DisputeTicket
        {
            Id = created.Id,
            TradeTransactionId = tradeId,
            ComplainantId = buyerId,
            Reason = "Sim mất sóng đột ngột",
            Status = DisputeStatus.OPEN
        };
        mockUow.Setup(u => u.DisputeTickets.GetByIdAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        var resolveHandler = new ResolveDisputeCommandHandler(mockUow.Object, mockNotif.Object);

        // Act
        var resolved = await resolveHandler.Handle(
            new ResolveDisputeCommand(DisputeStatus.RESOLVED_FAVOR_BUYER, "Chấp nhận hoàn tiền", ticket.Id, Guid.NewGuid()),
            CancellationToken.None);

        // Assert
        created.Id.Should().NotBeEmpty();
        resolved.Should().BeTrue();
        mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task TradeDeadlineReminderService_ShouldSendDeduplicatedRemindersAndSkipDisputedTrades()
    {
        // Arrange
        await using var context = TestTrustSimDbContext.CreateInMemory();
        var utcNow = new DateTime(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

        var sellerA = Guid.NewGuid();
        var buyerA = Guid.NewGuid();
        var auction1Id = Guid.NewGuid();
        var auction2Id = Guid.NewGuid();
        var auction3Id = Guid.NewGuid();
        var auction4Id = Guid.NewGuid();
        var tradeSellerDueId = Guid.NewGuid();
        var tradeBuyerDueId = Guid.NewGuid();
        var tradeAdminDueId = Guid.NewGuid();
        var tradeDisputedId = Guid.NewGuid();
        var pendingProofId = Guid.NewGuid();

        var sim = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0912345678" };
        await context.UserAccounts.AddRangeAsync(
            new UserAccount { Id = sellerA, UserName = "rem_seller", Email = "s@rem.vn" },
            new UserAccount { Id = buyerA, UserName = "rem_buyer", Email = "b@rem.vn" });
        await context.SimNumbers.AddAsync(sim);
        await context.Auctions.AddRangeAsync(
            new AuctionEntity { Id = auction1Id, AuctionCode = "AUC-REM-1", AuthorId = sellerA, SimNumberId = sim.Id, AuctionStatus = AuctionStatus.END },
            new AuctionEntity { Id = auction2Id, AuctionCode = "AUC-REM-2", AuthorId = sellerA, SimNumberId = sim.Id, AuctionStatus = AuctionStatus.END },
            new AuctionEntity { Id = auction3Id, AuctionCode = "AUC-REM-3", AuthorId = sellerA, SimNumberId = sim.Id, AuctionStatus = AuctionStatus.END },
            new AuctionEntity { Id = auction4Id, AuctionCode = "AUC-REM-4", AuthorId = sellerA, SimNumberId = sim.Id, AuctionStatus = AuctionStatus.END });

        await context.TradeTransactions.AddRangeAsync(
            new TradeTransaction
            {
                Id = tradeSellerDueId,
                AuctionId = auction1Id,
                SellerId = sellerA,
                BuyerId = buyerA,
                SimNumberId = sim.Id,
                TradeTransactionCode = "TRD-REM-SELLER",
                TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                TransferDeadline = utcNow.AddHours(20),
                SimTransfer = new SimTransfer { Id = Guid.NewGuid(), TradeTransactionId = tradeSellerDueId, SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION }
            },
            new TradeTransaction
            {
                Id = tradeBuyerDueId,
                AuctionId = auction2Id,
                SellerId = sellerA,
                BuyerId = buyerA,
                SimNumberId = sim.Id,
                TradeTransactionCode = "TRD-REM-BUYER",
                TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                LiquidationDeadline = utcNow.AddHours(10),
                SimTransfer = new SimTransfer { Id = Guid.NewGuid(), TradeTransactionId = tradeBuyerDueId, SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION }
            },
            new TradeTransaction
            {
                Id = tradeAdminDueId,
                AuctionId = auction3Id,
                SellerId = sellerA,
                BuyerId = buyerA,
                SimNumberId = sim.Id,
                TradeTransactionCode = "TRD-REM-ADMIN",
                TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                TransferDeadline = utcNow.AddHours(18),
                SimTransfer = CreateTransferWithOldPendingProof(tradeAdminDueId, auction3Id, sellerA, pendingProofId, utcNow.AddHours(-14))
            },
            new TradeTransaction
            {
                Id = tradeDisputedId,
                AuctionId = auction4Id,
                SellerId = sellerA,
                BuyerId = buyerA,
                SimNumberId = sim.Id,
                TradeTransactionCode = "TRD-REM-DISPUTE",
                TradeTransactionStatus = TradeTransactionStatus.PendingTransfer,
                TransferDeadline = utcNow.AddHours(5),
                SimTransfer = new SimTransfer { Id = Guid.NewGuid(), TradeTransactionId = tradeDisputedId, SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION }
            });

        await context.DisputeTickets.AddAsync(new DisputeTicket
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeDisputedId,
            ComplainantId = buyerA,
            Reason = "Disputed",
            Status = DisputeStatus.OPEN
        });
        await context.SaveChangesAsync();

        var mockNotif = new Mock<INotificationService>();
        mockNotif.Setup(n => n.SendNotificationAsync(
                It.IsAny<Guid>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .Returns((Guid uid, NotificationType t, string title, string content, string? url, string? rt, Guid? rid, object? meta, bool email, CancellationToken ct) =>
                PersistFakeNotificationAsync(context, uid, t, title, content, url, rt, rid, meta, ct));

        mockNotif.Setup(n => n.SendNotificationToRolesAsync(
                It.IsAny<IReadOnlyCollection<UserRole>>(),
                It.IsAny<NotificationType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<bool>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<UserRole> roles, NotificationType t, string title, string content, string? url, string? rt, Guid? rid, object? meta, bool email, Guid? ex, CancellationToken ct) =>
                PersistFakeStaffNotificationAsync(context, t, title, content, url, rt, rid, meta, ct));

        var reminderService = new TradeDeadlineReminderService(
            context,
            mockNotif.Object,
            Options.Create(new TradeDeadlineSettings()),
            NullLogger<TradeDeadlineReminderService>.Instance);

        // Act 1: First sweep sends Seller (24H), Buyer (12H), and Admin (pending review) reminders; skips Disputed trade
        var sentFirst = await reminderService.ProcessDueRemindersAsync(utcNow);

        // Assert 1
        sentFirst.Should().Be(3);
        mockNotif.Verify(n => n.SendNotificationAsync(
            sellerA, NotificationType.TRADE_DEADLINE_REMINDER, It.IsAny<string>(), It.IsAny<string>(),
            $"/trade/{tradeSellerDueId}", "TradeTransaction", tradeSellerDueId, It.IsAny<object?>(), true, It.IsAny<CancellationToken>()), Times.Once);
        mockNotif.Verify(n => n.SendNotificationAsync(
            buyerA, NotificationType.TRADE_DEADLINE_REMINDER, It.IsAny<string>(), It.IsAny<string>(),
            $"/trade/{tradeBuyerDueId}", "TradeTransaction", tradeBuyerDueId, It.IsAny<object?>(), true, It.IsAny<CancellationToken>()), Times.Once);
        mockNotif.Verify(n => n.SendNotificationToRolesAsync(
            It.IsAny<IReadOnlyCollection<UserRole>>(), NotificationType.TRADE_DEADLINE_REMINDER, It.IsAny<string>(), It.IsAny<string>(),
            $"/transfer-review?tradeId={tradeAdminDueId}", "TradeTransaction", tradeAdminDueId, It.IsAny<object?>(), false, null, It.IsAny<CancellationToken>()), Times.Once);
        mockNotif.Verify(n => n.SendNotificationAsync(
            It.IsAny<Guid>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), tradeDisputedId, It.IsAny<object?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);

        // Act 2: Repeat sweep 15 minutes later must deduplicate (0 new reminders)
        var sentSecond = await reminderService.ProcessDueRemindersAsync(utcNow.AddMinutes(15));
        sentSecond.Should().Be(0);
    }

    private static SimTransfer CreateTransferWithOldPendingProof(Guid tradeId, Guid auctionId, Guid sellerId, Guid proofId, DateTime createdUtc)
    {
        var st = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };
        st.AddTransferProof(new TransferProof
        {
            Id = proofId,
            AuctionId = auctionId,
            SimTransferId = st.Id,
            SubmittedByUserId = sellerId,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            CreatedDate = createdUtc
        });
        return st;
    }

    private static async Task<Notification> PersistFakeNotificationAsync(
        TrustSimDbContext context,
        Guid userId,
        NotificationType type,
        string title,
        string content,
        string? url,
        string? refType,
        Guid? refId,
        object? meta,
        CancellationToken ct)
    {
        var notif = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            NotificationType = type,
            Title = title,
            Content = content,
            ActionUrl = url,
            ReferenceType = refType,
            ReferenceId = refId,
            MetadataJson = meta != null ? JsonSerializer.Serialize(meta) : null,
            CreatedDate = DateTime.UtcNow
        };
        await context.Notifications.AddAsync(notif, ct);
        await context.SaveChangesAsync(ct);
        return notif;
    }

    private static async Task<int> PersistFakeStaffNotificationAsync(
        TrustSimDbContext context,
        NotificationType type,
        string title,
        string content,
        string? url,
        string? refType,
        Guid? refId,
        object? meta,
        CancellationToken ct)
    {
        await PersistFakeNotificationAsync(context, Guid.NewGuid(), type, title, content, url, refType, refId, meta, ct);
        return 1;
    }
}
