using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.API.Controllers;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Queries;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Trade;

public class SimTransferProofAndReviewTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICloudStorageService> _mockCloudStorage;
    private readonly Mock<INotificationService> _mockNotification;
    private readonly Mock<IAuctionDelayQueueService> _mockDelayQueue;
    private readonly Mock<ILedgerService> _mockLedgerService;
    private readonly Mock<IPdfContractService> _mockPdfContractService;
    private readonly Mock<IPdfSigningService> _mockPdfSigningService;

    public SimTransferProofAndReviewTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCloudStorage = new Mock<ICloudStorageService>();
        _mockNotification = new Mock<INotificationService>();
        _mockDelayQueue = new Mock<IAuctionDelayQueueService>();
        _mockLedgerService = new Mock<ILedgerService>();
        _mockPdfContractService = new Mock<IPdfContractService>();
        _mockPdfSigningService = new Mock<IPdfSigningService>();
    }

    private SubmitMultipleTransferProofsCommandHandler CreateSubmitMultipleHandler()
    {
        return new SubmitMultipleTransferProofsCommandHandler(
            _mockUow.Object,
            _mockCloudStorage.Object,
            _mockNotification.Object,
            _mockDelayQueue.Object,
            Options.Create(new TradeDeadlineSettings { LiquidationSigningDeadlineHours = 72 }),
            NullLogger<SubmitMultipleTransferProofsCommandHandler>.Instance);
    }

    private ReviewSimTransferProofCommandHandler CreateReviewHandler()
    {
        return new ReviewSimTransferProofCommandHandler(
            _mockUow.Object,
            _mockLedgerService.Object,
            _mockNotification.Object,
            _mockPdfContractService.Object,
            _mockPdfSigningService.Object,
            _mockCloudStorage.Object,
            _mockDelayQueue.Object,
            NullLogger<ReviewSimTransferProofCommandHandler>.Instance);
    }

    private static IFormFile CreateFakeFormFile(string fileName = "proof.jpg", int size = 256)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(size);
        return mockFile.Object;
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_SellerHandover_SetsStatusAndLiquidationDeadlineAndEnqueuesDelayJob()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-SELLER-PROOF-01",
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0988111222" },
            Escrow = new Escrow { Id = Guid.NewGuid(), EscrowAmount = 20_000_000m, EscrowStatus = EscrowStatus.HELD }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        _mockCloudStorage.Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "sim-transfer", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.trustsim.vn/sim-transfer/seller_handover_1.jpg");

        var handler = CreateSubmitMultipleHandler();
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: sellerId,
            Files: new List<IFormFile> { CreateFakeFormFile("seller_handover_1.jpg") },
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Biên bản chuyển quyền tại điểm giao dịch Viettel"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SimTransferStatus.Should().Be(SimTransferStatus.SELLER_PROOF_SUBMITTED.ToString());
        result.Proofs.Should().ContainSingle();
        result.Proofs[0].ProofType.Should().Be(TransferProofType.SELLER_HANDOVER.ToString());
        result.Proofs[0].ProofStatus.Should().Be(ProofStatus.PENDING.ToString());
        trade.LiquidationDeadline.Should().NotBeNull();
        trade.LiquidationDeadline!.Value.Should().BeAfter(DateTime.UtcNow.AddHours(70));

        _mockDelayQueue.Verify(q => q.EnqueueJobAsync(
            It.Is<AuctionDelayJob>(j => j.AuctionId == auctionId &&
                                        j.JobType == AuctionDelayJobTypes.LIQUIDATION_DEADLINE &&
                                        j.ExtraData == tradeId.ToString()),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_BuyerReceipt_SetsStatusToBuyerProofSubmittedAndPreservesProofType()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION
        };
        simTransfer.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.APPROVED,
            ImageUrl = "https://cdn.trustsim.vn/sim-transfer/seller_approved.jpg"
        });

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-BUYER-PROOF-01",
            SimTransfer = simTransfer,
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0988333444" }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        _mockCloudStorage.Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "sim-transfer", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.trustsim.vn/sim-transfer/buyer_receipt_1.jpg");

        var handler = CreateSubmitMultipleHandler();
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: buyerId,
            Files: new List<IFormFile> { CreateFakeFormFile("buyer_receipt_1.jpg") },
            ProofType: TransferProofType.BUYER_RECEIPT,
            Description: "Ảnh chụp sóng và TTTB chính chủ trên My Viettel"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SimTransferStatus.Should().Be(SimTransferStatus.BUYER_PROOF_SUBMITTED.ToString());
        result.Proofs.Should().HaveCount(2);
        result.Proofs.Should().Contain(p =>
            p.ProofType == TransferProofType.BUYER_RECEIPT.ToString() &&
            p.ImageUrl == "https://cdn.trustsim.vn/sim-transfer/buyer_receipt_1.jpg");
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_BuyerReceiptWhenSellerNotYetApproved_ThrowsBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };
        simTransfer.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/sim-transfer/seller_pending.jpg"
        });

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-BUYER-EARLY-01",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateSubmitMultipleHandler();
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: buyerId,
            Files: new List<IFormFile> { CreateFakeFormFile("buyer_early.jpg") },
            ProofType: TransferProofType.BUYER_RECEIPT,
            Description: "Buyer nộp sớm khi Seller chưa được duyệt"
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Người Bán phải được Ban Quản Trị sàn phê duyệt thành công trước khi Người Mua nộp*");
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_BuyerAttemptsSellerHandoverProof_ThrowsForbiddenAccessException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-ROLE-MISMATCH"
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateSubmitMultipleHandler();
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: buyerId,
            Files: new List<IFormFile> { CreateFakeFormFile() },
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Buyer trying to upload seller handover"
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Người Bán*");
    }

    [Fact]
    public async Task GetPendingTransferReviews_FlatAndGroupedByAuction_ReturnsAccurateCountsAndFiltersByAuctionAndKeyword()
    {
        // Arrange
        var auctionId1 = Guid.NewGuid();
        var auctionId2 = Guid.NewGuid();
        var tradeId1 = Guid.NewGuid();
        var tradeId2 = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var simTransfer1 = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId1,
            SimTransferStatus = SimTransferStatus.BUYER_PROOF_SUBMITTED
        };
        simTransfer1.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer1.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/s1.jpg"
        });
        simTransfer1.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer1.Id,
            ProofType = TransferProofType.BUYER_RECEIPT,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/b1.jpg"
        });

        var simTransfer2 = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId2,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };
        simTransfer2.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer2.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.APPROVED,
            ImageUrl = "https://cdn.trustsim.vn/s2.jpg"
        });

        var trade1 = new TradeTransaction
        {
            Id = tradeId1,
            AuctionId = auctionId1,
            TradeTransactionCode = "TRD-AUC-001",
            SellerId = sellerId,
            BuyerId = buyerId,
            Seller = new UserAccount { Id = sellerId, UserName = "seller_vip", UserProfile = new UserProfile { FullName = "Trần Văn Bán" } },
            Buyer = new UserAccount { Id = buyerId, UserName = "buyer_vip", UserProfile = new UserProfile { FullName = "Lê Thị Mua" } },
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0988999999" },
            Escrow = new Escrow { Id = Guid.NewGuid(), EscrowAmount = 50_000_000m, EscrowStatus = EscrowStatus.HELD },
            SimTransfer = simTransfer1,
            Auction = new Auction
            {
                Id = auctionId1,
                AuctionCode = "AUC-2026-VIP01",
                AuctionType = AuctionType.ENGLISH,
                AuctionStatus = AuctionStatus.END,
                CurrentPrice = 50_000_000m,
                StartAt = DateTime.UtcNow.AddDays(-2),
                EndAt = DateTime.UtcNow.AddDays(-1)
            }
        };

        var trade2 = new TradeTransaction
        {
            Id = tradeId2,
            AuctionId = auctionId2,
            TradeTransactionCode = "TRD-AUC-002",
            SellerId = sellerId,
            BuyerId = buyerId,
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0912345678" },
            Escrow = new Escrow { Id = Guid.NewGuid(), EscrowAmount = 15_000_000m, EscrowStatus = EscrowStatus.HELD },
            SimTransfer = simTransfer2,
            Auction = new Auction
            {
                Id = auctionId2,
                AuctionCode = "AUC-2026-DUTCH02",
                AuctionType = AuctionType.DUTCH,
                AuctionStatus = AuctionStatus.END,
                CurrentPrice = 15_000_000m
            }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetPendingReviewsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TradeTransaction> { trade1, trade2 });

        var handler = new GetPendingTransferReviewsQueryHandler(_mockUow.Object);

        // Act
        var flatFilteredByKeyword = await handler.Handle(
            new GetPendingTransferReviewsQuery(Keyword: "0988999999"),
            CancellationToken.None);

        var groupedByAuction = await handler.Handle(
            new GetPendingTransferReviewsGroupedByAuctionQuery(AuctionId: auctionId1),
            CancellationToken.None);

        // Assert - Flat query with keyword filter
        flatFilteredByKeyword.Should().ContainSingle();
        var item = flatFilteredByKeyword[0];
        item.AuctionId.Should().Be(auctionId1);
        item.AuctionCode.Should().Be("AUC-2026-VIP01");
        item.AuctionType.Should().Be(AuctionType.ENGLISH.ToString());
        item.AuctionStatus.Should().Be(AuctionStatus.END.ToString());
        item.WinningPrice.Should().Be(50_000_000m);
        item.PendingSellerProofCount.Should().Be(1);
        item.PendingBuyerProofCount.Should().Be(1);

        // Assert - Grouped by Auction query
        groupedByAuction.Should().ContainSingle();
        var group = groupedByAuction[0];
        group.AuctionId.Should().Be(auctionId1);
        group.AuctionCode.Should().Be("AUC-2026-VIP01");
        group.TotalTradesCount.Should().Be(1);
        group.TotalPendingProofsCount.Should().Be(2);
        group.PendingSellerProofCount.Should().Be(1);
        group.PendingBuyerProofCount.Should().Be(1);
        group.SellerProofs.Should().ContainSingle(p => p.ProofType == TransferProofType.SELLER_HANDOVER.ToString());
        group.BuyerProofs.Should().ContainSingle(p => p.ProofType == TransferProofType.BUYER_RECEIPT.ToString());
        group.Trades.Should().ContainSingle(t => t.TradeId == tradeId1);
    }

    [Fact]
    public async Task ReviewSimTransferProof_ReverseOrder_ApproveBuyerFirstThenApproveSellerSecond_CompletesTransferAndSealsDocument3AndReleasesEscrow()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var simNumberId = Guid.NewGuid();

        var simNumber = new SimNumber
        {
            Id = simNumberId,
            PhoneNumber = "0988777666",
            SimStatus = SimStatus.IN_AUCTION
        };

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.BUYER_PROOF_SUBMITTED
        };

        var sellerProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/seller-handover-rev.jpg"
        };
        var buyerProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.BUYER_RECEIPT,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/buyer-receipt-rev.jpg"
        };
        simTransfer.AddTransferProof(sellerProof);
        simTransfer.AddTransferProof(buyerProof);

        var transferContract = new TransferContract
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            ContractStatus = ContractStatus.FULLY_SIGNED,
            IsAutoEnforced = false
        };

        var escrow = new Escrow
        {
            Id = Guid.NewGuid(),
            EscrowAmount = 20_000_000m,
            EscrowStatus = EscrowStatus.HELD
        };

        var auction = new Auction
        {
            Id = auctionId,
            AuctionCode = "AUC-REV-001",
            AuctionRule = new AuctionRule
            {
                Id = Guid.NewGuid(),
                SellerDepositAmount = 2_000_000m
            }
        };

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            Auction = auction,
            TradeTransactionCode = "TRD-REV-ORDER-001",
            SellerId = sellerId,
            BuyerId = buyerId,
            SimNumberId = simNumberId,
            SimNumber = simNumber,
            SimTransfer = simTransfer,
            TransferContract = transferContract,
            Escrow = escrow,
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer
        };

        var sellerWallet = new Wallet
        {
            Id = Guid.NewGuid(),
            UserAccountId = sellerId,
            AvailableBalance = 10_000_000m,
            LockedBalance = 2_000_000m,
            WalletStatus = WalletStatus.ACTIVE
        };

        var oldOwnership = new SimOwnership
        {
            Id = Guid.NewGuid(),
            SimNumberId = simNumberId,
            UserAccountId = sellerId,
            OwnershipStatus = OwnershipStatus.ACTIVE,
            AcquistionType = AcquistionType.INITIAL_VERIFICATION
        };

        var signedAt = DateTime.UtcNow;
        var padesResult = new PadesSignResult(
            SignedPdfBytes: new byte[] { 0x25, 0x50, 0x44, 0x46 },
            Sha256Hash: "sha256-reverse-order-doc3",
            CertificateSerial: "TS-CERT-REV",
            SignedAt: signedAt,
            TsaTimestamp: signedAt,
            TsaAuthority: "TrustSim TSA"
        );

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        _mockUow.Setup(u => u.Wallets.GetByUserIdAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerWallet);
        _mockUow.Setup(u => u.SimOwnerships.GetActiveOwnershipBySimNumberIdAsync(simNumberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldOwnership);

        _mockPdfContractService.Setup(p => p.GenerateLiquidationMinutePdfAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 10, 20, 30 });
        _mockPdfContractService.Setup(p => p.GetStampBoxesAsync(tradeId, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SignatureStampBoxDto>());
        _mockPdfSigningService.Setup(s => s.SignPdfPadesAsync(
                It.IsAny<byte[]>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<SignatureStampBoxDto>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(padesResult);
        _mockCloudStorage.Setup(c => c.UploadImageBytesAsync(
                padesResult.SignedPdfBytes,
                It.IsAny<string>(),
                "contracts",
                "application/pdf",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://cdn.trustsim.vn/contracts/BienBanThanhLy_ReverseOrder.pdf");

        _mockLedgerService.Setup(l => l.PostEscrowReleaseAsync(
                sellerWallet.Id,
                20_000_000m,
                1_000_000m,
                escrow.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerJournal { Id = Guid.NewGuid(), JournalCode = "JRN-REL-REV" });

        _mockLedgerService.Setup(l => l.PostSellerListingDepositReleaseAsync(
                sellerWallet.Id,
                2_000_000m,
                auctionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LedgerJournal { Id = Guid.NewGuid(), JournalCode = "JRN-SELLER-DEP-REL" });

        var handler = CreateReviewHandler();

        // Act Step 1: Admin attempts to approve BUYER first while Seller is still PENDING -> Must throw BadRequestException
        Func<Task> actBuyerFirst = async () => await handler.Handle(new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "BUYER",
            IsApproved: true,
            RejectionReason: null
        ), CancellationToken.None);

        await actBuyerFirst.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Phải phê duyệt bằng chứng chuyển nhượng của Người Bán trước*");
        buyerProof.ProofStatus.Should().Be(ProofStatus.PENDING);

        // Act Step 2: Admin approves SELLER first -> Seller becomes APPROVED, status moves to PENDING_BUYER_ACTION, Escrow still HELD
        var step1Result = await handler.Handle(new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        ), CancellationToken.None);

        sellerProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        step1Result.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION.ToString());
        trade.Escrow.EscrowStatus.Should().Be(EscrowStatus.HELD);

        // Act Step 3: Admin approves BUYER second (after Seller is APPROVED) -> Full completion triggered!
        var step2Result = await handler.Handle(new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "BUYER",
            IsApproved: true,
            RejectionReason: null
        ), CancellationToken.None);

        // Assert Step 3: Full completion triggered after strict sequential Seller -> Buyer approval
        buyerProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        step2Result.SimTransferStatus.Should().Be(SimTransferStatus.COMPLETED.ToString());
        step2Result.LiquidationDocumentUrl.Should().Be("https://cdn.trustsim.vn/contracts/BienBanThanhLy_ReverseOrder.pdf");
        step2Result.NetSellerPayout.Should().Be(19_000_000m);

        trade.TradeTransactionStatus.Should().Be(TradeTransactionStatus.Completed);
        trade.TransferContract.ContractStatus.Should().Be(ContractStatus.COMPLETED);
        trade.TransferContract.LiquidationDocumentHash.Should().Be("sha256-reverse-order-doc3");
        trade.TransferContract.LiquidationSealedAt.Should().Be(signedAt);

        oldOwnership.OwnershipStatus.Should().Be(OwnershipStatus.ENDED);
        trade.SimNumber.SimStatus.Should().Be(SimStatus.TRANSFERRED);
        trade.Escrow.EscrowStatus.Should().Be(EscrowStatus.RELEASED);

        _mockLedgerService.Verify(l => l.PostEscrowReleaseAsync(
            sellerWallet.Id,
            20_000_000m,
            1_000_000m,
            escrow.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockLedgerService.Verify(l => l.PostSellerListingDepositReleaseAsync(
            sellerWallet.Id,
            2_000_000m,
            auctionId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewSimTransferProof_RejectThenResubmit_OnlyApprovesNewPendingProofsAndPreservesRejectedHistory()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };

        var oldRejectedProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            Description = "Ảnh lần 1 bị mờ",
            ImageUrl = "https://cdn.trustsim.vn/seller-blurry.jpg"
        };
        simTransfer.AddTransferProof(oldRejectedProof);

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-RESUBMIT-001",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();

        // Act 1: Reject the first proof
        await handler.Handle(new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "SELLER",
            IsApproved: false,
            RejectionReason: "Ảnh bị lóa sáng"
        ), CancellationToken.None);

        oldRejectedProof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_SELLER_ACTION);

        // Arrange 2: Seller uploads a new clear proof
        var newPendingProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            Description = "Ảnh lần 2 rõ nét",
            ImageUrl = "https://cdn.trustsim.vn/seller-clear.jpg"
        };
        simTransfer.AddTransferProof(newPendingProof);
        trade.SimTransfer.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;

        // Act 2: Admin approves Seller proofs
        await handler.Handle(new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        ), CancellationToken.None);

        // Assert: Old rejected proof remains REJECTED; new pending proof becomes APPROVED
        oldRejectedProof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        newPendingProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION);
    }

    [Fact]
    public async Task GetSimTransferDetailsQueryHandler_PreservesProofTypeAndAllowsBuyerToSubmitProofAndAdminToView()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION
        };
        simTransfer.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.APPROVED,
            ImageUrl = "https://cdn.trustsim.vn/s.jpg"
        });
        simTransfer.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.BUYER_RECEIPT,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/b.jpg"
        });

        var trade = new TradeTransaction
        {
            Id = tradeId,
            TradeTransactionCode = "TRD-DETAILS-001",
            SellerId = sellerId,
            BuyerId = buyerId,
            SimTransfer = simTransfer,
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0909123123" }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new GetSimTransferDetailsQueryHandler(_mockUow.Object);

        // Act
        var buyerView = await handler.Handle(new GetSimTransferDetailsQuery(tradeId, buyerId, IsAdmin: false), CancellationToken.None);
        var adminView = await handler.Handle(new GetSimTransferDetailsQuery(tradeId, adminId, IsAdmin: true), CancellationToken.None);

        // Assert
        buyerView.IsBuyer.Should().BeTrue();
        buyerView.CanSubmitProof.Should().BeTrue();
        buyerView.Proofs.Should().HaveCount(2);
        buyerView.Proofs.Should().Contain(p => p.ProofType == TransferProofType.SELLER_HANDOVER.ToString());
        buyerView.Proofs.Should().Contain(p => p.ProofType == TransferProofType.BUYER_RECEIPT.ToString());

        adminView.TradeId.Should().Be(tradeId);
        adminView.Proofs.Should().HaveCount(2);
    }

    [Fact]
    public async Task SimTransfersController_AuctionGroupedEndpoints_ReturnExpectedOkAndNotFoundResponses()
    {
        // Arrange
        var auctionId = Guid.NewGuid();
        var missingAuctionId = Guid.NewGuid();
        var mockMediator = new Mock<IMediator>();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(c => c.UserId).Returns(Guid.NewGuid());
        mockCurrentUser.Setup(c => c.Role).Returns("ADMIN");

        var sampleGroup = new AuctionPendingTransferReviewGroupDto(
            AuctionId: auctionId,
            AuctionCode: "AUC-CTRL-001",
            AuctionType: "ENGLISH",
            AuctionStatus: "ENDED",
            SimPhoneNumber: "0988000111",
            WinningPrice: 12_000_000m,
            AuctionStartAt: DateTime.UtcNow.AddDays(-2),
            AuctionEndAt: DateTime.UtcNow.AddDays(-1),
            TotalTradesCount: 1,
            TotalPendingProofsCount: 2,
            PendingSellerProofCount: 1,
            PendingBuyerProofCount: 1,
            HasApprovedSeller: false,
            HasApprovedBuyer: false,
            SellerProofs: new List<TransferProofDto>(),
            BuyerProofs: new List<TransferProofDto>(),
            Trades: new List<PendingTransferReviewDto>());

        mockMediator
            .Setup(m => m.Send(It.Is<GetPendingTransferReviewsGroupedByAuctionQuery>(q => q.AuctionId == auctionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuctionPendingTransferReviewGroupDto> { sampleGroup });

        mockMediator
            .Setup(m => m.Send(It.Is<GetPendingTransferReviewsGroupedByAuctionQuery>(q => q.AuctionId == missingAuctionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuctionPendingTransferReviewGroupDto>());

        var services = new ServiceCollection();
        services.AddSingleton(mockMediator.Object);
        services.AddSingleton(mockCurrentUser.Object);
        var serviceProvider = services.BuildServiceProvider();

        var controller = new SimTransfersController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = serviceProvider
                }
            }
        };

        // Act
        var groupedResult = await controller.GetPendingReviewsGroupedByAuction(auctionId, "0988000111");
        var singleAuctionOkResult = await controller.GetPendingReviewByAuction(auctionId);
        var singleAuctionNotFoundResult = await controller.GetPendingReviewByAuction(missingAuctionId);

        // Assert
        var okGrouped = groupedResult.Should().BeOfType<OkObjectResult>().Subject;
        var groupedResponse = okGrouped.Value.Should().BeOfType<ApiResponse<List<AuctionPendingTransferReviewGroupDto>>>().Subject;
        groupedResponse.Success.Should().BeTrue();
        groupedResponse.Data.Should().ContainSingle(g => g.AuctionId == auctionId);

        var okSingle = singleAuctionOkResult.Should().BeOfType<OkObjectResult>().Subject;
        var singleResponse = okSingle.Value.Should().BeOfType<ApiResponse<AuctionPendingTransferReviewGroupDto>>().Subject;
        singleResponse.Success.Should().BeTrue();
        singleResponse.Data!.AuctionCode.Should().Be("AUC-CTRL-001");

        var notFoundSingle = singleAuctionNotFoundResult.Should().BeOfType<NotFoundObjectResult>().Subject;
        var notFoundResponse = notFoundSingle.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        notFoundResponse.Success.Should().BeFalse();
    }
}
