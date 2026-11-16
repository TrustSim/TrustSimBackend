using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Tests.Common;
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

    private SubmitMultipleTransferProofsCommandHandler CreateSubmitMultipleHandler(IUnitOfWork? uow = null)
    {
        return new SubmitMultipleTransferProofsCommandHandler(
            uow ?? _mockUow.Object,
            _mockCloudStorage.Object,
            _mockNotification.Object,
            _mockDelayQueue.Object,
            Options.Create(new TradeDeadlineSettings { LiquidationSigningDeadlineHours = 72 }),
            NullLogger<SubmitMultipleTransferProofsCommandHandler>.Instance);
    }

    private WithdrawTransferProofCommandHandler CreateWithdrawHandler(IUnitOfWork? uow = null)
    {
        return new WithdrawTransferProofCommandHandler(
            uow ?? _mockUow.Object,
            _mockDelayQueue.Object,
            NullLogger<WithdrawTransferProofCommandHandler>.Instance);
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
        mockFile.Setup(f => f.ContentType).Returns("image/jpeg");
        mockFile.Setup(f => f.Length).Returns(size);
        return mockFile.Object;
    }

    private static TransferProof CreateProofWithImages(
        Guid auctionId,
        Guid simTransferId,
        Guid submitterId,
        TransferProofType proofType,
        ProofStatus status,
        params string[] imageUrls)
    {
        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            SimTransferId = simTransferId,
            SubmittedByUserId = submitterId,
            ProofType = proofType,
            ProofStatus = status,
            VerifiedAt = status == ProofStatus.PENDING ? null : DateTime.UtcNow
        };

        for (var i = 0; i < imageUrls.Length; i++)
        {
            proof.AddImage(new TransferProofImage
            {
                Id = Guid.NewGuid(),
                TransferProofId = proof.Id,
                ImageUrl = imageUrls[i],
                FileName = $"img_{i + 1}.jpg",
                ContentType = "image/jpeg",
                SortOrder = i
            });
        }

        return proof;
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_RealEfCoreInMemory_TwoImagesCreatesOneProofWithTwoImagesAndNoFourDuplicate()
    {
        // Arrange: Test trực tiếp với TrustSimDbContext InMemory thật để kiểm chứng EF Core Relationship Fixup
        await using var context = TestTrustSimDbContext.CreateInMemory();
        var repo = new TradeTransactionRepository(context);
        var uowMock = new Mock<IUnitOfWork>();
        uowMock.Setup(u => u.TradeTransactions).Returns(repo);
        uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => context.SaveChangesAsync(ct));

        var auctionId = Guid.NewGuid();
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var simNumberId = Guid.NewGuid();

        var sellerUser = new UserAccount { Id = sellerId, UserName = "seller_ef", Email = "seller@ef.test" };
        var buyerUser = new UserAccount { Id = buyerId, UserName = "buyer_ef", Email = "buyer@ef.test" };
        var carrier = new SimCarrier { Id = Guid.NewGuid(), CarrierName = "Viettel", CarrierCode = "VTL" };
        var prefix = new PhonePrefix { Id = Guid.NewGuid(), PrefixValue = "098", SimCarrierId = carrier.Id };
        var simNumber = new SimNumber { Id = simNumberId, PhoneNumber = "0988111222", PhonePrefixId = prefix.Id };
        var auction = new Auction
        {
            Id = auctionId,
            AuctionCode = "AUC-EF-001",
            AuthorId = sellerId,
            SimNumberId = simNumberId,
            AuctionStatus = AuctionStatus.END
        };
        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            SellerId = sellerId,
            BuyerId = buyerId,
            SimNumberId = simNumberId,
            TradeTransactionCode = "TRD-EF-001",
            TradeTransactionStatus = TradeTransactionStatus.PendingTransfer
        };
        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
        };

        await context.UserAccounts.AddRangeAsync(sellerUser, buyerUser);
        await context.SimCarriers.AddAsync(carrier);
        await context.PhonePrefixes.AddAsync(prefix);
        await context.SimNumbers.AddAsync(simNumber);
        await context.Auctions.AddAsync(auction);
        await context.TradeTransactions.AddAsync(trade);
        await context.SimTransfers.AddAsync(simTransfer);
        await context.SaveChangesAsync();

        var uploadSeq = 0;
        _mockCloudStorage
            .Setup(c => c.UploadImageAsync(It.IsAny<IFormFile>(), "sim-transfer", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => $"https://cdn.trustsim.vn/sim-transfer/proof_{++uploadSeq}.jpg");

        var handler = CreateSubmitMultipleHandler(uowMock.Object);
        var idempotencyKey = "idem-ef-2-images-001";
        var command = new SubmitMultipleTransferProofsByAuctionCommand(
            AuctionId: auctionId,
            UserId: sellerId,
            Files: new List<IFormFile>
            {
                CreateFakeFormFile("front.jpg", 1024),
                CreateFakeFormFile("back.jpg", 2048)
            },
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Upload 2 ảnh biên bản tại quầy",
            IdempotencyKey: idempotencyKey);

        // Act 1: Nộp 2 ảnh lần đầu
        var result1 = await handler.Handle(command, CancellationToken.None);

        // Assert 1: Không bị nhân đôi thành 4 ảnh; DB chỉ có 1 TransferProof + 2 TransferProofImage
        result1.Proofs.Should().ContainSingle();
        result1.Proofs[0].AuctionId.Should().Be(auctionId);
        result1.Proofs[0].Images.Should().HaveCount(2);
        result1.HasPendingSellerProof.Should().BeTrue();
        result1.CanSubmitProof.Should().BeFalse();

        var dbProofs = await context.TransferProofs.Include(p => p.Images).ToListAsync();
        dbProofs.Should().ContainSingle();
        dbProofs[0].Images.Should().HaveCount(2);
        dbProofs[0].IdempotencyKey.Should().Be(idempotencyKey);

        // Act 2: Gửi lại liên tiếp cùng IdempotencyKey (double-submit)
        var result2 = await handler.Handle(command, CancellationToken.None);

        // Assert 2: Không upload thêm lên S3, DB vẫn chỉ có 1 TransferProof và 2 TransferProofImage
        _mockCloudStorage.Verify(
            c => c.UploadImageAsync(It.IsAny<IFormFile>(), "sim-transfer", It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        result2.Proofs.Should().ContainSingle();
        result2.Proofs[0].Images.Should().HaveCount(2);
        (await context.TransferProofs.CountAsync()).Should().Be(1);
        (await context.TransferProofImages.CountAsync()).Should().Be(2);

        // Act 3: Gửi với IdempotencyKey khác khi bộ cũ đang PENDING -> bị chặn
        var duplicatePendingCmd = command with { IdempotencyKey = "idem-ef-different-key" };
        Func<Task> actWhilePending = async () => await handler.Handle(duplicatePendingCmd, CancellationToken.None);
        await actWhilePending.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*thu hồi*");

        // Act 4: Thu hồi bộ đang PENDING rồi upload lại bộ mới (2 ảnh khác)
        var withdrawHandler = CreateWithdrawHandler(uowMock.Object);
        var withdrawResult = await withdrawHandler.Handle(
            new WithdrawTransferProofCommand(dbProofs[0].Id, sellerId),
            CancellationToken.None);

        withdrawResult.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_SELLER_ACTION.ToString());
        withdrawResult.HasPendingSellerProof.Should().BeFalse();
        withdrawResult.CanSubmitProof.Should().BeTrue();
        withdrawResult.Proofs.Should().ContainSingle(p => p.ProofStatus == ProofStatus.WITHDRAWN.ToString());

        var resubmitResult = await handler.Handle(duplicatePendingCmd, CancellationToken.None);
        resubmitResult.Proofs.Should().HaveCount(2); // 1 WITHDRAWN + 1 PENDING mới
        resubmitResult.Proofs.Count(p => p.ProofStatus == ProofStatus.PENDING.ToString()).Should().Be(1);
        (await context.TransferProofs.CountAsync()).Should().Be(2);
        (await context.TransferProofImages.CountAsync()).Should().Be(4);
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
            Files: new List<IFormFile>
            {
                CreateFakeFormFile("seller_handover_1.jpg"),
                CreateFakeFormFile("seller_handover_2.jpg")
            },
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Biên bản chuyển quyền tại điểm giao dịch Viettel"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SimTransferStatus.Should().Be(SimTransferStatus.SELLER_PROOF_SUBMITTED.ToString());
        result.Proofs.Should().ContainSingle();
        result.Proofs[0].AuctionId.Should().Be(auctionId);
        result.Proofs[0].ProofType.Should().Be(TransferProofType.SELLER_HANDOVER.ToString());
        result.Proofs[0].ProofStatus.Should().Be(ProofStatus.PENDING.ToString());
        result.Proofs[0].Images.Should().HaveCount(2);
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
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION
        };
        simTransfer.AddTransferProof(CreateProofWithImages(
            auctionId,
            simTransfer.Id,
            sellerId,
            TransferProofType.SELLER_HANDOVER,
            ProofStatus.APPROVED,
            "https://cdn.trustsim.vn/sim-transfer/seller_approved.jpg"));

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
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
            p.Images.Any(i => i.ImageUrl == "https://cdn.trustsim.vn/sim-transfer/buyer_receipt_1.jpg"));
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_BuyerReceiptWhenSellerNotYetApproved_ThrowsBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };
        simTransfer.AddTransferProof(CreateProofWithImages(
            auctionId,
            simTransfer.Id,
            sellerId,
            TransferProofType.SELLER_HANDOVER,
            ProofStatus.PENDING,
            "https://cdn.trustsim.vn/sim-transfer/seller_pending.jpg"));

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
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
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
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
        simTransfer1.AddTransferProof(CreateProofWithImages(
            auctionId1, simTransfer1.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/s1_a.jpg", "https://cdn.trustsim.vn/s1_b.jpg"));
        simTransfer1.AddTransferProof(CreateProofWithImages(
            auctionId1, simTransfer1.Id, buyerId, TransferProofType.BUYER_RECEIPT, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/b1.jpg"));

        var simTransfer2 = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId2,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };
        simTransfer2.AddTransferProof(CreateProofWithImages(
            auctionId2, simTransfer2.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.APPROVED,
            "https://cdn.trustsim.vn/s2.jpg"));

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

        // Assert - Flat query
        flatFilteredByKeyword.Should().ContainSingle();
        var item = flatFilteredByKeyword[0];
        item.AuctionId.Should().Be(auctionId1);
        item.AuctionCode.Should().Be("AUC-2026-VIP01");
        item.PendingSellerProofCount.Should().Be(1);
        item.PendingBuyerProofCount.Should().Be(1);
        item.PendingSellerImageCount.Should().Be(2);
        item.PendingBuyerImageCount.Should().Be(1);

        // Assert - Grouped by Auction query
        groupedByAuction.Should().ContainSingle();
        var group = groupedByAuction[0];
        group.AuctionId.Should().Be(auctionId1);
        group.TotalPendingProofsCount.Should().Be(2);
        group.TotalPendingImagesCount.Should().Be(3);
        group.PendingSellerImageCount.Should().Be(2);
        group.PendingBuyerImageCount.Should().Be(1);
        group.SellerProofs.Should().ContainSingle(p => p.ProofType == TransferProofType.SELLER_HANDOVER.ToString() && p.Images.Count == 2);
        group.BuyerProofs.Should().ContainSingle(p => p.ProofType == TransferProofType.BUYER_RECEIPT.ToString() && p.Images.Count == 1);
    }

    [Fact]
    public async Task ReviewSimTransferProof_SequentialOrder_BuyerBeforeSellerThrows_ThenSellerThenBuyerCompletesTradeAndSealsDocument3()
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

        var sellerProof = CreateProofWithImages(
            auctionId, simTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/seller-handover-1.jpg", "https://cdn.trustsim.vn/seller-handover-2.jpg");
        var buyerProof = CreateProofWithImages(
            auctionId, simTransfer.Id, buyerId, TransferProofType.BUYER_RECEIPT, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/buyer-receipt-1.jpg");
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
            TsaAuthority: "TrustSim TSA");

        _mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(sellerProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerProof);
        _mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(buyerProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(buyerProof);
        _mockUow.Setup(u => u.TradeTransactions.GetByAuctionIdAsync(auctionId, It.IsAny<CancellationToken>()))
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

        // Act Step 1: Duyệt BUYER trước khi SELLER được duyệt -> phải chặn
        Func<Task> actBuyerFirst = async () => await handler.Handle(new ReviewSimTransferProofCommand(
            TransferProofId: buyerProof.Id,
            AdminUserId: adminId,
            IsApproved: true,
            RejectionReason: null), CancellationToken.None);

        await actBuyerFirst.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Phải phê duyệt bằng chứng chuyển nhượng của Người Bán trước*");
        buyerProof.ProofStatus.Should().Be(ProofStatus.PENDING);

        // Act Step 2: Duyệt 1 bản ghi TransferProof của SELLER (gồm 2 ảnh con)
        var step1Result = await handler.Handle(new ReviewSimTransferProofCommand(
            TransferProofId: sellerProof.Id,
            AdminUserId: adminId,
            IsApproved: true,
            RejectionReason: null), CancellationToken.None);

        sellerProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        sellerProof.VerifiedByAdminId.Should().Be(adminId);
        sellerProof.Images.Should().HaveCount(2);
        step1Result.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION.ToString());
        trade.Escrow.EscrowStatus.Should().Be(EscrowStatus.HELD);

        // Act Step 3: Duyệt BUYER -> hoàn tất, sinh VB 3 & giải ngân
        var step2Result = await handler.Handle(new ReviewSimTransferProofCommand(
            TransferProofId: buyerProof.Id,
            AdminUserId: adminId,
            IsApproved: true,
            RejectionReason: null), CancellationToken.None);

        buyerProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        step2Result.SimTransferStatus.Should().Be(SimTransferStatus.COMPLETED.ToString());
        step2Result.LiquidationDocumentUrl.Should().Be("https://cdn.trustsim.vn/contracts/BienBanThanhLy_ReverseOrder.pdf");
        step2Result.NetSellerPayout.Should().Be(19_000_000m);

        trade.TradeTransactionStatus.Should().Be(TradeTransactionStatus.Completed);
        trade.TransferContract.ContractStatus.Should().Be(ContractStatus.COMPLETED);
        oldOwnership.OwnershipStatus.Should().Be(OwnershipStatus.ENDED);
        trade.SimNumber.SimStatus.Should().Be(SimStatus.TRANSFERRED);
        trade.Escrow.EscrowStatus.Should().Be(EscrowStatus.RELEASED);
    }

    [Fact]
    public async Task ReviewSimTransferProof_RejectThenResubmit_UpdatesSingleProofWithRejectionReasonAndPreservesHistory()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED
        };

        var oldRejectedProof = CreateProofWithImages(
            auctionId, simTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/seller-blurry-1.jpg", "https://cdn.trustsim.vn/seller-blurry-2.jpg");
        oldRejectedProof.Description = "Ảnh lần 1";
        simTransfer.AddTransferProof(oldRejectedProof);

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-RESUBMIT-001",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(oldRejectedProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldRejectedProof);
        _mockUow.Setup(u => u.TradeTransactions.GetByAuctionIdAsync(auctionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();

        // Act 1: Reject bộ ảnh đầu tiên -> chỉ 1 bản ghi TransferProof bị REJECTED, Description gốc giữ nguyên, RejectionReason lưu riêng
        await handler.Handle(new ReviewSimTransferProofCommand(
            TransferProofId: oldRejectedProof.Id,
            AdminUserId: adminId,
            IsApproved: false,
            RejectionReason: "Ảnh bị lóa sáng"), CancellationToken.None);

        oldRejectedProof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        oldRejectedProof.RejectionReason.Should().Be("Ảnh bị lóa sáng");
        oldRejectedProof.Description.Should().Be("Ảnh lần 1");
        oldRejectedProof.Images.Should().HaveCount(2);
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_SELLER_ACTION);

        // Arrange 2: Seller nộp bộ mới rõ nét
        var newPendingProof = CreateProofWithImages(
            auctionId, simTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/seller-clear.jpg");
        newPendingProof.Description = "Ảnh lần 2 rõ nét";
        simTransfer.AddTransferProof(newPendingProof);
        trade.SimTransfer.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;

        _mockUow.Setup(u => u.TradeTransactions.GetTransferProofByIdAsync(newPendingProof.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(newPendingProof);

        // Act 2: Admin duyệt bộ mới
        await handler.Handle(new ReviewSimTransferProofCommand(
            TransferProofId: newPendingProof.Id,
            AdminUserId: adminId,
            IsApproved: true,
            RejectionReason: null), CancellationToken.None);

        // Assert
        oldRejectedProof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        newPendingProof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION);
    }

    [Fact]
    public async Task GetSimTransferDetailsQueryHandler_PreservesProofTypeAndAllowsBuyerToSubmitProofAndAdminToView()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION
        };
        simTransfer.AddTransferProof(CreateProofWithImages(
            auctionId, simTransfer.Id, sellerId, TransferProofType.SELLER_HANDOVER, ProofStatus.APPROVED,
            "https://cdn.trustsim.vn/s.jpg"));
        simTransfer.AddTransferProof(CreateProofWithImages(
            auctionId, simTransfer.Id, buyerId, TransferProofType.BUYER_RECEIPT, ProofStatus.PENDING,
            "https://cdn.trustsim.vn/b.jpg"));

        var trade = new TradeTransaction
        {
            Id = tradeId,
            AuctionId = auctionId,
            TradeTransactionCode = "TRD-DETAILS-001",
            SellerId = sellerId,
            BuyerId = buyerId,
            SimTransfer = simTransfer,
            SimNumber = new SimNumber { Id = Guid.NewGuid(), PhoneNumber = "0909123123" }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new GetSimTransferDetailsQueryHandler(
            _mockUow.Object,
            _mockPdfContractService.Object,
            _mockPdfSigningService.Object,
            _mockCloudStorage.Object,
            NullLogger<GetSimTransferDetailsQueryHandler>.Instance);

        // Act
        var buyerView = await handler.Handle(new GetSimTransferDetailsQuery(tradeId, buyerId, IsAdmin: false), CancellationToken.None);
        var adminView = await handler.Handle(new GetSimTransferDetailsQuery(tradeId, adminId, IsAdmin: true), CancellationToken.None);

        // Assert: Buyer đang có 1 proof PENDING nên CanSubmitProof = false (phải thu hồi nếu muốn nộp lại)
        buyerView.IsBuyer.Should().BeTrue();
        buyerView.HasPendingBuyerProof.Should().BeTrue();
        buyerView.CanSubmitProof.Should().BeFalse();
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
            TotalPendingImagesCount: 3,
            PendingSellerImageCount: 2,
            PendingBuyerImageCount: 1,
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
