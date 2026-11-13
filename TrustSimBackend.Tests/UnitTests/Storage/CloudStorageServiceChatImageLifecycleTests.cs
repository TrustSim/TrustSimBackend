using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Infrastructure.Services;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Storage;

public class CloudStorageServiceChatImageLifecycleTests
{
    [Fact]
    public async Task UploadChatImageAsync_WhenConfiguredWith7DaysRetention_ShouldCreateChatBucketConfigure7DayLifecycleAndUploadImage()
    {
        // Arrange
        var s3Mock = new Mock<IAmazonS3>();
        var uniqueBucketName = $"chat-images-{Guid.NewGuid():N}";
        var conversationId = Guid.NewGuid();

        var settings = new MinioSettings
        {
            Endpoint = "http://localhost:9000",
            PublicUrl = "http://localhost:9000",
            Bucket = "pdf-files",
            ChatImageBucket = uniqueBucketName,
            ChatImageExpirationDays = 7
        };

        s3Mock.Setup(s => s.PutBucketAsync(It.IsAny<PutBucketRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutBucketResponse());
        s3Mock.Setup(s => s.PutBucketPolicyAsync(It.IsAny<PutBucketPolicyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutBucketPolicyResponse());
        s3Mock.Setup(s => s.PutLifecycleConfigurationAsync(It.IsAny<PutLifecycleConfigurationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutLifecycleConfigurationResponse());
        s3Mock.Setup(s => s.PutObjectAsync(It.IsAny<PutObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PutObjectResponse());

        var sut = new CloudStorageService(
            NullLogger<CloudStorageService>.Instance,
            s3Mock.Object,
            Options.Create(settings));

        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var stream = new MemoryStream(pngBytes);
        var formFile = new FormFile(stream, 0, pngBytes.Length, "file", "chat_photo.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        // Act
        var url = await sut.UploadChatImageAsync(formFile, conversationId, CancellationToken.None);

        // Assert
        url.Should().StartWith($"http://localhost:9000/{uniqueBucketName}/conversations/{conversationId:N}/");
        url.Should().EndWith(".png");

        s3Mock.Verify(s => s.PutLifecycleConfigurationAsync(
            It.Is<PutLifecycleConfigurationRequest>(req =>
                req.BucketName == uniqueBucketName &&
                req.Configuration.Rules.Any(r =>
                    r.Status == LifecycleRuleStatus.Enabled &&
                    r.Expiration.Days == 7)),
            It.IsAny<CancellationToken>()), Times.Once);

        s3Mock.Verify(s => s.PutObjectAsync(
            It.Is<PutObjectRequest>(req =>
                req.BucketName == uniqueBucketName &&
                req.ContentType == "image/png" &&
                req.Key.StartsWith($"conversations/{conversationId:N}/")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteFileAsync_WhenUrlBelongsToChatImagesBucket_ShouldResolveChatImagesBucketAndDeleteObject()
    {
        // Arrange
        var s3Mock = new Mock<IAmazonS3>();
        var settings = new MinioSettings
        {
            PublicUrl = "http://localhost:9000",
            Bucket = "pdf-files",
            ChatImageBucket = "chat-images",
            ChatImageExpirationDays = 7
        };

        s3Mock.Setup(s => s.DeleteObjectAsync(It.IsAny<DeleteObjectRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeleteObjectResponse());

        var sut = new CloudStorageService(
            NullLogger<CloudStorageService>.Instance,
            s3Mock.Object,
            Options.Create(settings));

        var chatImageUrl = "http://localhost:9000/chat-images/conversations/abc123/sample.webp";

        // Act
        var deleted = await sut.DeleteFileAsync(chatImageUrl, CancellationToken.None);

        // Assert
        deleted.Should().BeTrue();
        s3Mock.Verify(s => s.DeleteObjectAsync(
            It.Is<DeleteObjectRequest>(req =>
                req.BucketName == "chat-images" &&
                req.Key == "conversations/abc123/sample.webp"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
