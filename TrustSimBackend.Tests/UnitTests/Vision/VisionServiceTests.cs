using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text;
using TrustSimBackend.API.Controllers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Infrastructure.Services;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Vision;

public class VisionServiceTests
{
    private readonly Mock<IGoogleVisionService> _mockVisionService;
    private readonly VisionController _controller;

    public VisionServiceTests()
    {
        _mockVisionService = new Mock<IGoogleVisionService>();
        _controller = new VisionController(_mockVisionService.Object);
    }

    [Fact]
    public async Task DetectText_WhenFileIsNull_ShouldReturnBadRequest()
    {
        // Arrange & Act
        var result = await _controller.DetectText(null, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("hợp lệ");
    }

    [Fact]
    public async Task DetectText_WhenFileLengthIsZero_ShouldReturnBadRequest()
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(0);
        mockFile.Setup(f => f.FileName).Returns("empty.jpg");

        // Act
        var result = await _controller.DetectText(mockFile.Object, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("hợp lệ");
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("malicious.exe")]
    [InlineData("data.csv")]
    [InlineData("image.gif")]
    [InlineData("notes.txt")]
    public async Task DetectText_WhenFileExtensionNotAllowed_ShouldReturnBadRequest(string fileName)
    {
        // Arrange
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.FileName).Returns(fileName);

        // Act
        var result = await _controller.DetectText(mockFile.Object, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<object>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Contain("Định dạng tệp không được hỗ trợ");
    }

    [Fact]
    public async Task DetectText_WhenVisionOcrFails_ShouldReturnBadRequestWithErrorMessage()
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("fake-image-bytes");
        var mockFile = CreateMockImageFile("invalid_contract.jpg", bytes);

        var ocrErrorResult = new GoogleVisionOcrResult
        {
            IsSuccess = false,
            ErrorMessage = "Google Cloud Vision API quota exceeded."
        };

        _mockVisionService.Setup(v => v.DetectAllTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ocrErrorResult);

        // Act
        var result = await _controller.DetectText(mockFile.Object, CancellationToken.None);

        // Assert
        var badRequestResult = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        var apiResponse = badRequestResult.Value.Should().BeOfType<ApiResponse<GoogleVisionOcrResult>>().Subject;
        apiResponse.Success.Should().BeFalse();
        apiResponse.Message.Should().Be("Google Cloud Vision API quota exceeded.");
    }

    [Theory]
    [InlineData("test.jpg")]
    [InlineData("test.jpeg")]
    [InlineData("test.png")]
    [InlineData("test.webp")]
    [InlineData("test.bmp")]
    public async Task DetectText_WhenVisionOcrSucceeds_ShouldReturnOkWithOcrResult(string fileName)
    {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes("fake-sim-paperwork-image");
        var mockFile = CreateMockImageFile(fileName, bytes);

        var expectedResult = new GoogleVisionOcrResult
        {
            IsSuccess = true,
            FullText = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\nBIÊN BẢN CHUYỂN QUYỀN SỬ DỤNG SIM\nSố thuê bao: 0988888888",
            Lines = new List<GoogleVisionOcrLine>
            {
                new() { LineIndex = 1, Text = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", Confidence = 0.99f },
                new() { LineIndex = 2, Text = "BIÊN BẢN CHUYỂN QUYỀN SỬ DỤNG SIM", Confidence = 0.97f },
                new() { LineIndex = 3, Text = "Số thuê bao: 0988888888", Confidence = 0.98f }
            },
            RawJson = "[{\"description\": \"CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM\"}]"
        };

        _mockVisionService.Setup(v => v.DetectAllTextAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.DetectText(mockFile.Object, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var apiResponse = okResult.Value.Should().BeOfType<ApiResponse<GoogleVisionOcrResult>>().Subject;
        apiResponse.Success.Should().BeTrue();
        apiResponse.Data.Should().NotBeNull();
        apiResponse.Data!.IsSuccess.Should().BeTrue();
        apiResponse.Data.FullText.Should().Contain("0988888888");
        apiResponse.Data.Lines.Should().HaveCount(3);
        apiResponse.Data.Lines[2].Text.Should().Be("Số thuê bao: 0988888888");
        apiResponse.Message.Should().Be("Nhận diện văn bản OCR thành công.");
    }

    [Fact]
    public async Task GoogleVisionService_DetectAllTextAsync_WhenBytesNullOrEmpty_ShouldReturnFailureWithoutCallingApi()
    {
        // Arrange
        var settings = new GeminiSettings
        {
            ApiKey = "AIzaSyTestApiKey"
        };
        using var httpClient = new HttpClient();
        var service = new GoogleVisionService(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<GoogleVisionService>>());

        // Act
        var nullResult = await service.DetectAllTextAsync(null!);
        var emptyResult = await service.DetectAllTextAsync(Array.Empty<byte>());

        // Assert
        nullResult.IsSuccess.Should().BeFalse();
        nullResult.ErrorMessage.Should().Be("Dữ liệu hình ảnh trống.");

        emptyResult.IsSuccess.Should().BeFalse();
        emptyResult.ErrorMessage.Should().Be("Dữ liệu hình ảnh trống.");
    }

    [Fact]
    public async Task GoogleVisionService_WhenApiKeyMissing_ShouldNotThrowInConstructorAndReturnFailure()
    {
        // Arrange
        var settings = new GeminiSettings
        {
            ApiKey = string.Empty
        };
        using var httpClient = new HttpClient();

        var act = () => new GoogleVisionService(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<GoogleVisionService>>());

        // Assert constructor does not throw
        var service = act.Should().NotThrow().Subject;

        // Act
        var result = await service.DetectAllTextAsync(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

        // Assert OCR returns clean failure
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GoogleVisionService_WhenSettingsDefault_ShouldNotThrowInConstructor()
    {
        // Arrange
        var settings = new GeminiSettings();
        using var httpClient = new HttpClient();

        // Act
        var act = () => new GoogleVisionService(
            httpClient,
            Options.Create(settings),
            Mock.Of<ILogger<GoogleVisionService>>());

        // Assert
        act.Should().NotThrow();
    }

    private static Mock<IFormFile> CreateMockImageFile(string fileName, byte[] content)
    {
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.FileName).Returns(fileName);
        mockFile.Setup(f => f.Length).Returns(content.Length);
        mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream targetStream, CancellationToken _) => targetStream.WriteAsync(content, 0, content.Length));
        return mockFile;
    }
}
