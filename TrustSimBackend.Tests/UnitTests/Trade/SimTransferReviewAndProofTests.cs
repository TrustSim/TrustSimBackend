using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Trade;

public class SimTransferReviewAndProofTests
{
    private readonly Mock<IUnitOfWork> _mockUow;
    private readonly Mock<ICloudStorageService> _mockCloudStorage;
    private readonly Mock<INotificationService> _mockNotification;
    private readonly Mock<IAuctionDelayQueueService> _mockDelayQueue;
    private readonly Mock<ILedgerService> _mockLedgerService;
    private readonly Mock<IPdfContractService> _mockPdfContractService;
    private readonly Mock<IPdfSigningService> _mockPdfSigningService;

    public SimTransferReviewAndProofTests()
    {
        _mockUow = new Mock<IUnitOfWork> { DefaultValue = DefaultValue.Mock };
        _mockCloudStorage = new Mock<ICloudStorageService>();
        _mockNotification = new Mock<INotificationService>();
        _mockDelayQueue = new Mock<IAuctionDelayQueueService>();
        _mockLedgerService = new Mock<ILedgerService>();
        _mockPdfContractService = new Mock<IPdfContractService>();
        _mockPdfSigningService = new Mock<IPdfSigningService>();
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
            Mock.Of<ILogger<ReviewSimTransferProofCommandHandler>>());
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_WhenNoFiles_ShouldThrowBadRequest()
    {
        // Arrange
        var handler = new SubmitMultipleTransferProofsCommandHandler(
            _mockUow.Object,
            _mockCloudStorage.Object,
            _mockNotification.Object,
            _mockDelayQueue.Object,
            Options.Create(new TradeDeadlineSettings()),
            Mock.Of<ILogger<SubmitMultipleTransferProofsCommandHandler>>());

        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            Files: new List<IFormFile>(),
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Test"
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*ít nhất một hình ảnh*");
    }

    [Fact]
    public async Task SubmitMultipleTransferProofs_WhenUnauthorizedUser_ShouldThrowForbidden()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-TEST-001"
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = new SubmitMultipleTransferProofsCommandHandler(
            _mockUow.Object,
            _mockCloudStorage.Object,
            _mockNotification.Object,
            _mockDelayQueue.Object,
            Options.Create(new TradeDeadlineSettings()),
            Mock.Of<ILogger<SubmitMultipleTransferProofsCommandHandler>>());

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(100);

        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: intruderId,
            Files: new List<IFormFile> { mockFile.Object },
            ProofType: TransferProofType.SELLER_HANDOVER,
            Description: "Intruder handover"
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task ReviewSimTransferProof_SellerApproved_ShouldChangeStatusToPendingBuyerAction()
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

        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/proof1.jpg"
        };
        simTransfer.AddTransferProof(proof);

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-TEST-002",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();

        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION.ToString());
        proof.ProofStatus.Should().Be(ProofStatus.APPROVED);
        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenBuyerApprovedAndSellerAlreadyApproved_ShouldCompleteTransferAndReleaseEscrow()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var simNumberId = Guid.NewGuid();

        var simNumber = new SimNumber
        {
            Id = simNumberId,
            PhoneNumber = "0988888888",
            SimStatus = SimStatus.IN_AUCTION
        };

        var simTransfer = new SimTransfer
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            SimTransferStatus = SimTransferStatus.PENDING_BUYER_ACTION
        };

        var sellerProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.APPROVED,
            ImageUrl = "https://cdn.trustsim.vn/seller-handover.jpg",
            VerifiedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        simTransfer.AddTransferProof(sellerProof);

        var buyerProof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.BUYER_RECEIPT,
            ProofStatus = ProofStatus.PENDING,
            ImageUrl = "https://cdn.trustsim.vn/buyer-signal.jpg"
        };
        simTransfer.AddTransferProof(buyerProof);

        var transferContract = new TransferContract
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            ContractStatus = ContractStatus.DRAFT,
            IsAutoEnforced = true,
            LiquidationDocumentUrl = null
        };

        var escrow = new Escrow
        {
            Id = Guid.NewGuid(),
            EscrowAmount = 10_000_000m,
            EscrowStatus = EscrowStatus.HELD
        };

        var trade = new TradeTransaction
        {
            Id = tradeId,
            TradeTransactionCode = "TRD-COMPLETE-001",
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
            AvailableBalance = 5_000_000m,
            LockedBalance = 0m,
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
            Sha256Hash: "sample-sha256-liquidation-hash",
            CertificateSerial: "TS-CERT-20261107",
            SignedAt: signedAt,
            TsaTimestamp: signedAt,
            TsaAuthority: "TrustSim TSA Server"
        );

        var mockJournal = new LedgerJournal
        {
            Id = Guid.NewGuid(),
            JournalCode = "JRN-ESCROW-REL-001"
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        _mockUow.Setup(u => u.Wallets.GetByUserIdAsync(sellerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sellerWallet);
        _mockUow.Setup(u => u.SimOwnerships.GetActiveOwnershipBySimNumberIdAsync(simNumberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldOwnership);

        _mockPdfContractService.Setup(p => p.GenerateLiquidationMinutePdfAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
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
            .ReturnsAsync("https://cdn.trustsim.vn/contracts/liquidation_signed.pdf");

        _mockLedgerService.Setup(l => l.PostEscrowReleaseAsync(
            sellerWallet.Id,
            10_000_000m,
            500_000m,
            escrow.Id,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockJournal);

        var handler = CreateReviewHandler();

        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "BUYER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.COMPLETED);
        trade.TradeTransactionStatus.Should().Be(TradeTransactionStatus.Completed);
        trade.CompletedAt.Should().NotBeNull();

        trade.TransferContract.LiquidationDocumentUrl.Should().Be("https://cdn.trustsim.vn/contracts/liquidation_signed.pdf");
        trade.TransferContract.LiquidationDocumentHash.Should().Be("sample-sha256-liquidation-hash");
        trade.TransferContract.ContractStatus.Should().Be(ContractStatus.COMPLETED);
        trade.TransferContract.BlockchainTxHash.Should().StartWith("0x");

        oldOwnership.OwnershipStatus.Should().Be(OwnershipStatus.ENDED);

        _mockUow.Verify(u => u.SimOwnerships.AddAsync(
            It.Is<SimOwnership>(o => o.OwnershipStatus == OwnershipStatus.ACTIVE &&
                                     o.AcquistionType == AcquistionType.AUCTION_TRANSFER &&
                                     o.UserAccountId == buyerId &&
                                     o.SimNumberId == simNumberId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SimVerifications.AddAsync(
            It.Is<SimVerification>(v => v.VerificationStatus == VerificationStatus.VERIFIED &&
                                        v.VerificationMethod == "AUCTION_TRANSFER_APPROVED"),
            It.IsAny<CancellationToken>()), Times.Once);

        trade.SimNumber.SimStatus.Should().Be(SimStatus.TRANSFERRED);
        _mockUow.Verify(u => u.SimNumbers.Update(trade.SimNumber), Times.Once);

        _mockLedgerService.Verify(l => l.PostEscrowReleaseAsync(
            sellerWallet.Id,
            10_000_000m,
            500_000m,
            escrow.Id,
            It.IsAny<CancellationToken>()), Times.Once);

        trade.Escrow.EscrowStatus.Should().Be(EscrowStatus.RELEASED);
        trade.Escrow.ReleasedAt.Should().NotBeNull();

        _mockDelayQueue.Verify(q => q.CancelJobAsync(
            trade.AuctionId,
            AuctionDelayJobTypes.LIQUIDATION_DEADLINE,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockNotification.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.WALLET_TOPUP_SUCCESS,
            It.Is<string>(t => t.Contains("Tiền đã về ví")),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockNotification.Verify(n => n.SendNotificationAsync(
            buyerId,
            NotificationType.SYSTEM_ALERT,
            It.Is<string>(t => t.Contains("sở hữu SIM chính chủ")),
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        result.SimTransferStatus.Should().Be(SimTransferStatus.COMPLETED.ToString());
        result.LiquidationDocumentUrl.Should().Be("https://cdn.trustsim.vn/contracts/liquidation_signed.pdf");
        result.NetSellerPayout.Should().Be(9_500_000m);
    }

    [Fact]
    public async Task ReviewSimTransferProof_SellerRejected_ShouldChangeStatusToPendingSellerActionAndMarkProofRejected()
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

        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.PENDING,
            Description = "Ảnh chụp giấy bàn giao nhà mạng",
            ImageUrl = "https://cdn.trustsim.vn/seller-doc.jpg"
        };
        simTransfer.AddTransferProof(proof);

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-REJECT-001",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();

        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "SELLER",
            IsApproved: false,
            RejectionReason: "Biên bản mờ không rõ chữ, thiếu con dấu nhà mạng"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_SELLER_ACTION);
        proof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        proof.Description.Should().Contain("[Từ chối: Biên bản mờ không rõ chữ, thiếu con dấu nhà mạng]");
        proof.VerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        _mockNotification.Verify(n => n.SendNotificationAsync(
            sellerId,
            NotificationType.TRADE_TRANSFERRING_SIM,
            It.IsAny<string>(),
            It.Is<string>(c => c.Contains("Biên bản mờ không rõ chữ, thiếu con dấu nhà mạng")),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_SELLER_ACTION.ToString());
    }

    [Fact]
    public async Task ReviewSimTransferProof_BuyerRejected_ShouldChangeStatusToPendingBuyerActionAndMarkProofRejected()
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
            SimTransferStatus = SimTransferStatus.BUYER_PROOF_SUBMITTED
        };
        simTransfer.AddTransferProof(new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.SELLER_HANDOVER,
            ProofStatus = ProofStatus.APPROVED,
            ImageUrl = "https://cdn.trustsim.vn/seller-approved.jpg"
        });

        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            SimTransferId = simTransfer.Id,
            ProofType = TransferProofType.BUYER_RECEIPT,
            ProofStatus = ProofStatus.PENDING,
            Description = "Ảnh chụp màn hình app My Viettel",
            ImageUrl = "https://cdn.trustsim.vn/buyer-proof.jpg"
        };
        simTransfer.AddTransferProof(proof);

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SellerId = sellerId,
            BuyerId = buyerId,
            TradeTransactionCode = "TRD-REJECT-002",
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();

        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: adminId,
            TargetParty: "BUYER",
            IsApproved: false,
            RejectionReason: "Ảnh kiểm tra sóng không hiển thị đúng số thuê bao"
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        trade.SimTransfer.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION);
        proof.ProofStatus.Should().Be(ProofStatus.REJECTED);
        proof.Description.Should().Contain("[Từ chối: Ảnh kiểm tra sóng không hiển thị đúng số thuê bao]");
        proof.VerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));

        _mockNotification.Verify(n => n.SendNotificationAsync(
            buyerId,
            NotificationType.TRADE_TRANSFERRING_SIM,
            It.IsAny<string>(),
            It.Is<string>(c => c.Contains("Ảnh kiểm tra sóng không hiển thị đúng số thuê bao")),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Guid?>(),
            It.IsAny<object?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        result.SimTransferStatus.Should().Be(SimTransferStatus.PENDING_BUYER_ACTION.ToString());
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenNoSellerProofs_ShouldThrowBadRequest()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var trade = new TradeTransaction
        {
            Id = tradeId,
            SimTransfer = new SimTransfer
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = tradeId,
                SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
            }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: Guid.NewGuid(),
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Người Bán chưa nộp bất kỳ bằng chứng sang tên nào*");
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenNoBuyerProofs_ShouldThrowBadRequest()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
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
            ImageUrl = "https://cdn.trustsim.vn/seller-approved.jpg"
        });

        var trade = new TradeTransaction
        {
            Id = tradeId,
            SimTransfer = simTransfer
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: Guid.NewGuid(),
            TargetParty: "BUYER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Người Mua chưa nộp bất kỳ ảnh nghiệm thu nào*");
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenTradeNotFound_ShouldThrowNotFound()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TradeTransaction?)null);

        var handler = CreateReviewHandler();
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: Guid.NewGuid(),
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*{tradeId}*");
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenTransferAlreadyCompleted_ShouldThrowBadRequest()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var trade = new TradeTransaction
        {
            Id = tradeId,
            SimTransfer = new SimTransfer
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = tradeId,
                SimTransferStatus = SimTransferStatus.COMPLETED
            }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: Guid.NewGuid(),
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*đã hoàn tất thành công trước đó*");
    }

    [Fact]
    public async Task ReviewSimTransferProof_WhenTransferCancelled_ShouldThrowBadRequest()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var trade = new TradeTransaction
        {
            Id = tradeId,
            SimTransfer = new SimTransfer
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = tradeId,
                SimTransferStatus = SimTransferStatus.CANCELLED
            }
        };

        _mockUow.Setup(u => u.TradeTransactions.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var handler = CreateReviewHandler();
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: Guid.NewGuid(),
            TargetParty: "SELLER",
            IsApproved: true,
            RejectionReason: null
        );

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*đã bị hủy bỏ*");
    }
}
