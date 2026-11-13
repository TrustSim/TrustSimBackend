using System.Net;
using System.Text;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.Queries;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Services;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Trade;

public class VnptSmartCaSigningTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<ITradeTransactionRepository> _tradeRepoMock;
    private readonly Mock<ISmartCaService> _smartCaServiceMock;
    private readonly Mock<IContractSigningQueueService> _signingQueueMock;
    private readonly Mock<IAuctionDelayQueueService> _delayQueueMock;

    public VnptSmartCaSigningTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _tradeRepoMock = new Mock<ITradeTransactionRepository>();
        _smartCaServiceMock = new Mock<ISmartCaService>();
        _signingQueueMock = new Mock<IContractSigningQueueService>();
        _delayQueueMock = new Mock<IAuctionDelayQueueService>();

        _uowMock.Setup(u => u.TradeTransactions).Returns(_tradeRepoMock.Object);
    }

    [Fact]
    public async Task SmartCaService_GetUserCertificatesAsync_WhenGatewayReturns200_ShouldParseCertificateAndChainCorrectly()
    {
        // Arrange
        var responseJson = """
        {
          "status_code": 200,
          "message": "Success",
          "data": {
            "user_certificates": [
              {
                "service_type": "SMARTCA",
                "service_name": "SMARTCA PERSONAL PRO",
                "cert_id": "fcfffddf-2bed-4dda-8276-d00b4c3df6c9",
                "cert_status": "Đang hoạt động",
                "serial_number": "54010101493c47d39f8a84b30ed55191",
                "cert_subject": "C=VN,ST=HÀ NỘI,CN=Nguyễn Văn A,UID=CCCD:001099012345",
                "cert_valid_from": "2025-01-01T00:00:00Z",
                "cert_valid_to": "2028-01-01T00:00:00Z",
                "cert_data": "MIIFQjCCBCqgAwIBAg==",
                "chain_data": {
                  "ca_cert": "MIIGNDCCBBygAwI==",
                  "root_cert": "MIIG/DCCBOSgAw=="
                },
                "transaction_id": "SP_CA_09940"
              }
            ]
          }
        }
        """;

        using var httpClient = CreateStubHttpClient(HttpStatusCode.OK, responseJson);
        var settings = Options.Create(new SmartCaSettings
        {
            BaseUrl = "https://rmgateway.vnptit.vn/sca/sp769",
            EnableSandboxSimulation = false
        });

        var sut = new SmartCaService(httpClient, settings, NullLogger<SmartCaService>.Instance);

        // Act
        var certs = await sut.GetUserCertificatesAsync("001099012345", null, CancellationToken.None);

        // Assert
        certs.Should().HaveCount(1);
        certs[0].SerialNumber.Should().Be("54010101493c47d39f8a84b30ed55191");
        certs[0].CertStatus.Should().Be("Đang hoạt động");
        certs[0].CertDataBase64.Should().Be("MIIFQjCCBCqgAwIBAg==");
        certs[0].CaCertBase64.Should().Be("MIIGNDCCBBygAwI==");
    }

    [Fact]
    public async Task SmartCaService_GetUserCertificatesAsync_WhenGatewayReturns403CredentialInvalid_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var errorJson = """
        {
          "status_code": 403,
          "message": "CREDENTIAL_STATUS_INVALID: Chứng thư số đã hết hạn hoặc bị thu hồi"
        }
        """;

        using var httpClient = CreateStubHttpClient(HttpStatusCode.Forbidden, errorJson);
        var settings = Options.Create(new SmartCaSettings
        {
            BaseUrl = "https://rmgateway.vnptit.vn/sca/sp769",
            EnableSandboxSimulation = false
        });

        var sut = new SmartCaService(httpClient, settings, NullLogger<SmartCaService>.Instance);

        // Act
        Func<Task> act = async () => await sut.GetUserCertificatesAsync("001099012345", null, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*403 CREDENTIAL_STATUS_INVALID*");
    }

    [Fact]
    public async Task GetSmartCaCertificatesQuery_WhenCallerIsNeitherBuyerNorSeller_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var outsiderId = Guid.NewGuid();

        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);
        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var sut = new GetSmartCaCertificatesQueryHandler(_uowMock.Object, _smartCaServiceMock.Object);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new GetSmartCaCertificatesQuery(tradeId, outsiderId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Bên Mua hoặc Bên Bán*");
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenValidBuyerInitiatesV1AppConfirm_ShouldComputePdfSha256AndSavePendingSession()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var cert = new SmartCaCertificateDto(
            CertId: "cert-01",
            SerialNumber: "54010101493c47d39f8a84b30ed55191",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đang hoạt động",
            CertSubject: "CN=Người Mua,UID=CCCD:001099000111",
            ValidFrom: DateTime.UtcNow.AddMonths(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: "MIIG...",
            RootCertBase64: "MIIH...");

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync("001099000111", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { cert });

        var expectedTranCode = $"tran_{Guid.NewGuid():N}";
        _smartCaServiceMock.Setup(s => s.InitiateSignHashAsync(It.IsAny<SmartCaSignHashRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartCaSignHashRequestDto req, CancellationToken _) => new SmartCaSignInitiatedResultDto(
                IsSuccess: true,
                StatusCode: 200,
                Message: "sig_wait_for_user_confirm",
                TransactionId: req.TransactionId,
                TranCode: expectedTranCode,
                Sad: null,
                ExpiredIn: 300));

        var pdfServiceMock = new Mock<IPdfContractService>();
        pdfServiceMock.Setup(p => p.GenerateRawPdfBytesAsync(tradeId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("SAMPLE_PDF_BINARY_CONTENT"));

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance,
            pdfServiceMock.Object);

        // Act
        var result = await sut.Handle(
            new InitiateSmartCaContractSignCommand(
                TradeId: tradeId,
                UserId: buyerId,
                IpAddress: "127.0.0.1",
                UserAgent: "xUnit-Test"),
            CancellationToken.None);

        // Assert
        result.TradeId.Should().Be(tradeId);
        result.SignatureRole.Should().Be("BUYER");
        result.TranCode.Should().Be(expectedTranCode);
        result.SerialNumber.Should().Be("54010101493c47d39f8a84b30ed55191");
        result.DataToBeSignedHex.Should().HaveLength(64);
        result.IsCompletedImmediately.Should().BeFalse();
        result.DeepLinkUrl.Should().Be($"vnptsmartca://sign?tranCode={expectedTranCode}");

        var storedSession = SmartCaPendingSessionStore.TryGet(expectedTranCode);
        storedSession.Should().NotBeNull();
        storedSession!.TradeId.Should().Be(tradeId);
        storedSession.UserId.Should().Be(buyerId);
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenCertSubjectCompletelyMismatchesUserFullName_ShouldThrowBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var certWithMismatchName = new SmartCaCertificateDto(
            CertId: "cert-mismatch",
            SerialNumber: "54010101MISMATCH",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đang hoạt động",
            CertSubject: "C=VN,ST=HÀ NỘI,CN=Trần Thị Bích,UID=CCCD:001099000111",
            ValidFrom: DateTime.UtcNow.AddMonths(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync("001099000111", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { certWithMismatchName });

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new InitiateSmartCaContractSignCommand(
                TradeId: tradeId,
                UserId: buyerId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không trùng khớp với chủ sở hữu chứng thư số SmartCA*");
    }

    [Fact]
    public async Task CompleteSmartCaContractSign_WhenBothPartiesSign_ShouldTransitionToFullySignedEnqueuePadesSealingAndBeIdempotentOnDuplicateWebhook()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        // Giả sử Seller đã ký trước đó
        trade.TransferContract!.DigitalSignatures.Add(new DigitalSignature
        {
            Id = Guid.NewGuid(),
            TransferContractId = trade.TransferContract.Id,
            UserId = sellerId,
            SignatureRole = SignatureRole.SELLER,
            AuthMethod = "VNPT_SMARTCA",
            CryptographicSignature = "SELLER_SIG_BASE64",
            SignedAt = DateTime.UtcNow.AddMinutes(-5)
        });

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var buyerTranCode = $"tran_buyer_{Guid.NewGuid():N}";
        SmartCaPendingSessionStore.Save(new SmartCaPendingContractSession(
            TradeId: tradeId,
            UserId: buyerId,
            Role: SignatureRole.BUYER,
            CitizenId: "001099000111",
            SerialNumber: "54010101BUYER",
            TransactionId: $"TS_SCA_{Guid.NewGuid():N}",
            TranCode: buyerTranCode,
            DocId: $"HD-{trade.TradeTransactionCode}-BUYER",
            DataToBeSignedHex: "3cab0a7c77da32d278f4e85053176d847064cc84b0f1528aa96438a3edf92060",
            IpAddress: "10.0.0.1",
            UserAgent: "SmartCA-Test",
            CreatedAt: DateTime.UtcNow));

        var deadlineOptions = Options.Create(new TradeDeadlineSettings());

        var sut = new CompleteSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            _signingQueueMock.Object,
            _delayQueueMock.Object,
            deadlineOptions,
            NullLogger<CompleteSmartCaContractSignCommandHandler>.Instance);

        var command = new CompleteSmartCaContractSignCommand(
            TranCodeOrTransactionId: buyerTranCode,
            TradeId: tradeId,
            UserId: buyerId,
            SerialNumber: "54010101BUYER",
            WebhookSignatureValueBase64: "BUYER_CMS_SIGNATURE_BASE64_VALUE==",
            WebhookDocId: $"HD-{trade.TradeTransactionCode}-BUYER",
            WebhookStatusCode: 200);

        // Act 1: Webhook lần đầu hoàn tất chữ ký của Buyer
        var firstResult = await sut.Handle(command, CancellationToken.None);

        // Act 2: Webhook gọi lặp lại lần 2 (Idempotency Check)
        var duplicateResult = await sut.Handle(command, CancellationToken.None);

        // Assert
        firstResult.IsFullySigned.Should().BeTrue();
        firstResult.ContractStatus.Should().Be(ContractStatus.FULLY_SIGNED.ToString());
        trade.TradeTransactionStatus.Should().Be(TradeTransactionStatus.PendingTransfer);
        trade.TransferContract.DigitalSignatures.Should().HaveCount(2);

        duplicateResult.IsFullySigned.Should().BeTrue();
        trade.TransferContract.DigitalSignatures.Should().HaveCount(2);

        // Đảm bảo chỉ đẩy task niêm phong PAdES và Delay Queue đúng 1 lần duy nhất
        _signingQueueMock.Verify(q => q.EnqueueContractSigningTaskAsync(tradeId, false, It.IsAny<CancellationToken>()), Times.Once);
        _delayQueueMock.Verify(d => d.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.SIGNING_DEADLINE, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CompleteSmartCaContractSign_WhenUserRejectsOnSmartCaApp_ShouldThrowBadRequestExceptionAndNotPersistSignature()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var deadlineOptions = Options.Create(new TradeDeadlineSettings());
        var sut = new CompleteSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            _signingQueueMock.Object,
            _delayQueueMock.Object,
            deadlineOptions,
            NullLogger<CompleteSmartCaContractSignCommandHandler>.Instance);

        var rejectedWebhookCommand = new CompleteSmartCaContractSignCommand(
            TranCodeOrTransactionId: "tran_rejected_001",
            TradeId: tradeId,
            UserId: buyerId,
            WebhookStatusCode: 400);

        // Act
        Func<Task> act = async () => await sut.Handle(rejectedWebhookCommand, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*không thành công hoặc đã bị người dùng từ chối*");
        _tradeRepoMock.Verify(r => r.AddDigitalSignatureAsync(It.IsAny<DigitalSignature>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void InitiateSmartCaContractSignResultDto_DeepLinkUrl_ShouldFormatWithTranCode()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var sampleTranCode = "SC_SANDBOX_123456";

        // Act
        var dto = new InitiateSmartCaContractSignResultDto(
            TradeId: tradeId,
            TradeTransactionCode: "TRD-2026-0001",
            SignatureRole: "BUYER",
            TransactionId: "TS_SCA_001",
            TranCode: sampleTranCode,
            DocId: "HD-TRD-2026-0001-BUYER",
            DataToBeSignedHex: "3cab0a7c77da32d278f4e85053176d847064cc84b0f1528aa96438a3edf92060",
            SerialNumber: "54010101TEST",
            StatusMessage: "sig_wait_for_user_confirm",
            ExpiredInSeconds: 300);

        // Assert
        dto.DeepLinkUrl.Should().Be($"vnptsmartca://sign?tranCode={sampleTranCode}");
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenUserHasNoEkycCccd_ShouldThrowBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        // Người mua chưa hoàn tất eKYC (IdentityNumber trống)
        trade.Buyer!.UserProfile!.IdentityNumber = string.Empty;

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new InitiateSmartCaContractSignCommand(tradeId, buyerId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Tài khoản chưa có số CCCD xác thực eKYC*");
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenNoValidSmartCaCertFoundForCccd_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync(trade.Buyer!.UserProfile!.IdentityNumber!, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto>());

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new InitiateSmartCaContractSignCommand(tradeId, buyerId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*Không tìm thấy chứng thư số VNPT SmartCA hợp lệ*");
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenNameHasVietnameseAccentsAndCertSubjectIsUnaccentedUppercase_ShouldPassNameCrossCheckGuard()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        // User eKYC có dấu tiếng Việt
        trade.Buyer!.UserProfile!.FullName = "Nguyễn Văn Đại Gia";

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        // CertSubject từ VNPT SmartCA là không dấu in hoa
        var cert = new SmartCaCertificateDto(
            CertId: "cert-01",
            SerialNumber: "54010101ACCENT_OK",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đang hoạt động",
            CertSubject: "C=VN,ST=HÀ NỘI,CN=NGUYEN VAN DAI GIA,UID=CCCD:001099000111",
            ValidFrom: DateTime.UtcNow.AddMonths(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync("001099000111", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { cert });

        var expectedTranCode = $"tran_crosscheck_{Guid.NewGuid():N}";
        _smartCaServiceMock.Setup(s => s.InitiateSignHashAsync(It.IsAny<SmartCaSignHashRequestDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmartCaSignHashRequestDto req, CancellationToken _) => new SmartCaSignInitiatedResultDto(
                IsSuccess: true,
                StatusCode: 200,
                Message: "sig_wait_for_user_confirm",
                TransactionId: req.TransactionId,
                TranCode: expectedTranCode,
                Sad: null,
                ExpiredIn: 300));

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(
            new InitiateSmartCaContractSignCommand(tradeId, buyerId),
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SignatureRole.Should().Be("BUYER");
        result.TranCode.Should().Be(expectedTranCode);
        result.DeepLinkUrl.Should().Be($"vnptsmartca://sign?tranCode={expectedTranCode}");
    }

    [Fact]
    public async Task InitiateSmartCaContractSign_WhenCallerAlreadySignedContract_ShouldThrowBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        // Bên mua đã ký trước đó
        trade.TransferContract!.DigitalSignatures.Add(new DigitalSignature
        {
            Id = Guid.NewGuid(),
            TransferContractId = trade.TransferContract.Id,
            UserId = buyerId,
            SignatureRole = SignatureRole.BUYER,
            AuthMethod = "VNPT_SMARTCA",
            CryptographicSignature = "ALREADY_SIGNED_BASE64",
            SignedAt = DateTime.UtcNow.AddMinutes(-10)
        });

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var sut = new InitiateSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<InitiateSmartCaContractSignCommandHandler>.Instance);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new InitiateSmartCaContractSignCommand(tradeId, buyerId),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Bạn đã ký xác nhận hợp đồng này rồi*");
    }

    [Fact]
    public async Task CompleteSmartCaContractSign_WhenSignStatusIsPendingWaitConfirm_ShouldThrowBadRequestException()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var trade = CreateSampleTrade(tradeId, buyerId, sellerId);

        _tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);

        var pendingTranCode = $"tran_wait_{Guid.NewGuid():N}";
        SmartCaPendingSessionStore.Save(new SmartCaPendingContractSession(
            TradeId: tradeId,
            UserId: buyerId,
            Role: SignatureRole.BUYER,
            CitizenId: "001099000111",
            SerialNumber: "54010101BUYER",
            TransactionId: $"TS_SCA_{Guid.NewGuid():N}",
            TranCode: pendingTranCode,
            DocId: $"HD-{trade.TradeTransactionCode}-BUYER",
            DataToBeSignedHex: "3cab0a7c77da32d278f4e85053176d847064cc84b0f1528aa96438a3edf92060",
            IpAddress: "10.0.0.1",
            UserAgent: "SmartCA-Test",
            CreatedAt: DateTime.UtcNow));

        // SmartCA báo chưa hoàn tất (đang chờ người dùng bấm xác nhận FaceID/PIN)
        _smartCaServiceMock.Setup(s => s.GetSignTransactionStatusAsync(pendingTranCode, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmartCaSignStatusResultDto(
                IsCompleted: false,
                StatusCode: 200,
                Message: "sig_wait_for_user_confirm",
                TransactionId: "TS_SCA_001",
                Signatures: new List<SmartCaSignatureItemDto>()));

        var deadlineOptions = Options.Create(new TradeDeadlineSettings());
        var sut = new CompleteSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            _signingQueueMock.Object,
            _delayQueueMock.Object,
            deadlineOptions,
            NullLogger<CompleteSmartCaContractSignCommandHandler>.Instance);

        var command = new CompleteSmartCaContractSignCommand(
            TranCodeOrTransactionId: pendingTranCode,
            TradeId: tradeId,
            UserId: buyerId);

        // Act
        Func<Task> act = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*sig_wait_for_user_confirm*");
    }

    [Fact]
    public async Task CompleteSmartCaContractSign_WhenPendingSessionNotFoundAndTradeIdNull_ShouldThrowNotFoundException()
    {
        // Arrange
        var nonExistentTranCode = $"tran_nonexistent_{Guid.NewGuid():N}";
        var deadlineOptions = Options.Create(new TradeDeadlineSettings());
        var sut = new CompleteSmartCaContractSignCommandHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            _signingQueueMock.Object,
            _delayQueueMock.Object,
            deadlineOptions,
            NullLogger<CompleteSmartCaContractSignCommandHandler>.Instance);

        var command = new CompleteSmartCaContractSignCommand(
            TranCodeOrTransactionId: nonExistentTranCode,
            TradeId: null);

        // Act
        Func<Task> act = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage($"*Không tìm thấy phiên giao dịch ký số SmartCA ứng với mã '{nonExistentTranCode}'*");
    }

    private static TradeTransaction CreateSampleTrade(Guid tradeId, Guid buyerId, Guid sellerId)
    {
        var buyer = new UserAccount
        {
            Id = buyerId,
            UserName = "buyer_smartca",
            Email = "buyer@trustsim.vn",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = buyerId,
                FullName = "Nguyễn Văn Mua",
                IdentityNumber = "001099000111",
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        var seller = new UserAccount
        {
            Id = sellerId,
            UserName = "seller_smartca",
            Email = "seller@trustsim.vn",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = sellerId,
                FullName = "Trần Thị Bán",
                IdentityNumber = "001099000222",
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        var contract = new TransferContract
        {
            Id = Guid.NewGuid(),
            TradeTransactionId = tradeId,
            ContractStatus = ContractStatus.DRAFT
        };

        return new TradeTransaction
        {
            Id = tradeId,
            AuctionId = Guid.NewGuid(),
            TradeTransactionCode = "TRD-2026-9999",
            BuyerId = buyerId,
            Buyer = buyer,
            SellerId = sellerId,
            Seller = seller,
            TradeTransactionStatus = TradeTransactionStatus.PendingSignature,
            TransferContract = contract
        };
    }

    private static HttpClient CreateStubHttpClient(HttpStatusCode statusCode, string responseJson)
    {
        var handler = new StubHttpMessageHandler(statusCode, responseJson);
        return new HttpClient(handler);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseJson;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string responseJson)
        {
            _statusCode = statusCode;
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
