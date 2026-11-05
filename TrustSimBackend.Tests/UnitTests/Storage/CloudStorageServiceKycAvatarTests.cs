using System.Text;
using Amazon.S3;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Infrastructure.Services;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Storage;

public class CloudStorageServiceKycAvatarTests
{
    private readonly CloudStorageService _storageService;
    private readonly MinioSettings _settings;

    public CloudStorageServiceKycAvatarTests()
    {
        _settings = new MinioSettings
        {
            Endpoint = "http://localhost:9000",
            PublicUrl = "http://localhost:9000",
            AccessKey = "minioadmin",
            SecretKey = "minioadmin",
            Bucket = "pdf-files",
            UseSsl = false
        };

        var s3Config = new AmazonS3Config
        {
            ServiceURL = _settings.Endpoint,
            ForcePathStyle = true,
            UseHttp = true
        };
        var s3Client = new AmazonS3Client(_settings.AccessKey, _settings.SecretKey, s3Config);

        _storageService = new CloudStorageService(
            NullLogger<CloudStorageService>.Instance,
            s3Client,
            Options.Create(_settings));
    }

    [Fact]
    public async Task UploadAvatar_ShouldUploadSuccessfully_AndReturnPublicMinioUrl()
    {
        // Arrange
        var content = "dummy-avatar-image-binary-data"u8.ToArray();
        var stream = new MemoryStream(content);
        var formFile = new FormFile(stream, 0, content.Length, "file", "my_avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        // Act
        var avatarUrl = await _storageService.UploadImageAsync(formFile, "avatars");

        // Assert
        avatarUrl.Should().NotBeNullOrWhiteSpace();
        avatarUrl.Should().StartWith("http://localhost:9000/pdf-files/avatars/");
        avatarUrl.Should().EndWith(".png");

        // Verify delete cleans up
        var deleted = await _storageService.DeleteFileAsync(avatarUrl);
        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task UploadKycImages_ShouldUploadFrontAndSelfie_AndReturnValidUrls()
    {
        // Arrange
        var frontBytes = Encoding.UTF8.GetBytes("fake-cccd-front-card-bytes");
        var selfieBytes = Encoding.UTF8.GetBytes("fake-selfie-portrait-bytes");

        // Act - Simulate eKYC parallel upload in SubmitEkycVerificationCommandHandler
        var frontUrlTask = _storageService.UploadImageBytesAsync(frontBytes, "cccd_front.jpg", "kyc/front", "image/jpeg");
        var selfieUrlTask = _storageService.UploadImageBytesAsync(selfieBytes, "selfie.jpg", "kyc/selfie", "image/jpeg");

        await Task.WhenAll(frontUrlTask, selfieUrlTask);

        var frontUrl = await frontUrlTask;
        var selfieUrl = await selfieUrlTask;

        // Assert
        frontUrl.Should().StartWith("http://localhost:9000/pdf-files/kyc/front/");
        selfieUrl.Should().StartWith("http://localhost:9000/pdf-files/kyc/selfie/");

        // Clean up
        var deleteFront = await _storageService.DeleteFileAsync(frontUrl);
        var deleteSelfie = await _storageService.DeleteFileAsync(selfieUrl);

        deleteFront.Should().BeTrue();
        deleteSelfie.Should().BeTrue();
    }
}
