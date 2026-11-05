using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.Commands;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Services;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Pdf;

public class PdfFontValidatorTests
{
    private readonly Mock<ILogger<PdfFontValidatorService>> _loggerMock;
    private readonly PdfFontValidatorService _sut;

    public PdfFontValidatorTests()
    {
        _loggerMock = new Mock<ILogger<PdfFontValidatorService>>();
        _sut = new PdfFontValidatorService(_loggerMock.Object);
    }

    [Fact]
    public async Task AuditPdfStreamAsync_WhenAllFontsAreEmbeddedWithUnicode_ShouldReturnCrossDeviceSafeTrue()
    {
        // Arrange
        var vietnameseSample = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM - Độc lập Tự do Hạnh phúc - Nguyễn Thị Ánh Tuyết chuyển nhượng SIM 0988888888";
        var pdfBytes = PdfTestSampleBuilder.CreateUnicodeEmbeddedPdfBytes(vietnameseSample);
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _sut.AuditPdfStreamAsync(stream);

        // Assert
        result.IsCrossDeviceSafe.Should().BeTrue();
        result.TotalPages.Should().Be(1);
        result.TotalFonts.Should().BeGreaterThan(0);
        result.EmbeddedFontsCount.Should().Be(result.TotalFonts);
        result.UnembeddedFonts.Should().BeEmpty();
        result.LegacyNonUnicodeFonts.Should().BeEmpty();
        result.HasReplacementCharacters.Should().BeFalse();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task AuditPdfStreamAsync_WhenPdfContainsUnembeddedStandardFont_ShouldReturnCrossDeviceSafeFalse()
    {
        // Arrange
        var pdfBytes = PdfTestSampleBuilder.CreateUnembeddedStandardFontPdfBytes("Tai lieu dung font Helvetica khong nhung vao PDF");
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _sut.AuditPdfStreamAsync(stream);

        // Assert
        result.IsCrossDeviceSafe.Should().BeFalse();
        result.UnembeddedFonts.Should().Contain(f => f.Contains("Helvetica", StringComparison.OrdinalIgnoreCase));
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EnsureCrossDeviceCompatibleAsync_WhenPdfContainsUnembeddedFont_ShouldThrowInvalidPdfFontException()
    {
        // Arrange
        var pdfBytes = PdfTestSampleBuilder.CreateUnembeddedStandardFontPdfBytes("Tai lieu khong nhung font");
        using var stream = new MemoryStream(pdfBytes);

        // Act
        Func<Task> act = async () => await _sut.EnsureCrossDeviceCompatibleAsync(stream, "hop_dong_thieu_font.pdf");

        // Assert
        await act.Should().ThrowAsync<InvalidPdfFontException>()
            .WithMessage("*hop_dong_thieu_font.pdf*")
            .WithMessage("*Unembedded*");
    }

    [Fact]
    public async Task AuditPdfStreamAsync_WhenPdfUsesLegacyNonUnicodeFontName_ShouldDetectLegacyFontAndReturnUnsafe()
    {
        // Arrange
        var pdfBytes = PdfTestSampleBuilder.CreateLegacyNonUnicodeFontPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _sut.AuditPdfStreamAsync(stream);

        // Assert
        result.IsCrossDeviceSafe.Should().BeFalse();
        result.LegacyNonUnicodeFonts.Should().Contain("VNI-Times");
        result.Errors.Should().Contain(e => e.Contains("VNI-Times"));
    }

    [Fact]
    public async Task AuditPdfStreamAsync_WhenType0FontIsMissingToUnicodeCMap_ShouldPopulateMissingUnicodeWarning()
    {
        // Arrange
        var pdfBytes = PdfTestSampleBuilder.CreateMissingToUnicodeCMapPdfBytes();
        using var stream = new MemoryStream(pdfBytes);

        // Act
        var result = await _sut.AuditPdfStreamAsync(stream);

        // Assert
        result.MissingUnicodeMapFonts.Should().NotBeEmpty();
        result.Warnings.Should().Contain(w => w.Contains("/ToUnicode"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("NOT_A_PDF_HEADER_CONTENT")]
    public async Task AuditPdfStreamAsync_WhenStreamIsEmptyOrInvalidMagicBytes_ShouldThrowInvalidPdfFontException(string invalidContent)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(invalidContent);
        using var stream = new MemoryStream(bytes);

        // Act
        Func<Task> act = async () => await _sut.AuditPdfStreamAsync(stream);

        // Assert
        await act.Should().ThrowAsync<InvalidPdfFontException>();
    }

    [Fact]
    public async Task UploadDocumentCommandHandler_WhenUploadedPdfHasInvalidFonts_ShouldThrowInvalidPdfFontExceptionAndNotSaveToDatabase()
    {
        // Arrange
        var uowMock = new Mock<IUnitOfWork>();
        var docRepoMock = new Mock<IDocumentRepository>();
        var serviceProviderMock = new Mock<IServiceProvider>();
        var handlerLoggerMock = new Mock<ILogger<UploadDocumentCommandHandler>>();

        uowMock.Setup(u => u.Documents).Returns(docRepoMock.Object);

        var unembeddedPdfBytes = PdfTestSampleBuilder.CreateUnembeddedStandardFontPdfBytes("Unembedded PDF");
        var fileStream = new MemoryStream(unembeddedPdfBytes);
        var formFile = new FormFile(fileStream, 0, unembeddedPdfBytes.Length, "File", "tai_lieu_loi_font.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var handler = new UploadDocumentCommandHandler(
            uowMock.Object,
            serviceProviderMock.Object,
            _sut,
            handlerLoggerMock.Object);

        var command = new UploadDocumentCommand(
            File: formFile,
            DocumentName: "Tài liệu lỗi font",
            DocumentType: DocumentType.GENERAL);

        // Act
        Func<Task> act = async () => await handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidPdfFontException>()
            .WithMessage("*tai_lieu_loi_font.pdf*");
        docRepoMock.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);
        uowMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PdfContractService_GenerateRawPdfBytesAsync_ShouldEmbedAllVietnameseFontsAndPassCrossDeviceValidation()
    {
        // Arrange
        var tradeId = Guid.NewGuid();
        var tradeRepoMock = new Mock<ITradeTransactionRepository>();
        var uowMock = new Mock<IUnitOfWork>();
        var cloudStorageMock = new Mock<ICloudStorageService>();

        var trade = new TradeTransaction
        {
            Id = tradeId,
            TradeTransactionCode = "TRD-20261105-9999",
            Seller = new UserAccount
            {
                Id = Guid.NewGuid(),
                UserName = "seller_vn",
                Email = "seller@trustsim.vn",
                UserProfile = new UserProfile
                {
                    FullName = "Nguyễn Thị Ánh Tuyết",
                    IdentityNumber = "079199008888",
                    Address = "Số 12 Đường Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh",
                    PhoneNumber = "0901234567"
                }
            },
            Buyer = new UserAccount
            {
                Id = Guid.NewGuid(),
                UserName = "buyer_vn",
                Email = "buyer@trustsim.vn",
                UserProfile = new UserProfile
                {
                    FullName = "Đặng Quốc Cường",
                    IdentityNumber = "001095006666",
                    Address = "Phố Duy Tân, Quận Cầu Giấy, TP. Hà Nội",
                    PhoneNumber = "0987654321"
                }
            },
            SimNumber = new SimNumber
            {
                PhoneNumber = "0988888888",
                PhonePrefix = new PhonePrefix
                {
                    SimCarrier = new SimCarrier { CarrierName = "Viettel Telecom" }
                }
            },
            Escrow = new Escrow
            {
                EscrowAmount = 68_000_000m
            }
        };

        tradeRepoMock.Setup(r => r.GetByIdAsync(tradeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(trade);
        uowMock.Setup(u => u.TradeTransactions).Returns(tradeRepoMock.Object);

        var contractService = new PdfContractService(uowMock.Object, cloudStorageMock.Object);

        // Act
        var generatedPdfBytes = await contractService.GenerateRawPdfBytesAsync(tradeId, isAutoEnforced: false);
        using var pdfStream = new MemoryStream(generatedPdfBytes);
        var auditResult = await _sut.AuditPdfStreamAsync(pdfStream);

        // Assert
        auditResult.IsCrossDeviceSafe.Should().BeTrue();
        auditResult.UnembeddedFonts.Should().BeEmpty();
        auditResult.LegacyNonUnicodeFonts.Should().BeEmpty();
        auditResult.EmbeddedFontsCount.Should().BeGreaterThan(0);
    }
}
