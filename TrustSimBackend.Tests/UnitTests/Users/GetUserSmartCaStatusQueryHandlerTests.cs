using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Users.DTOs;
using TrustSimBackend.Application.Features.Users.Queries;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Users;

public class GetUserSmartCaStatusQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserAccountRepository> _userRepoMock;
    private readonly Mock<ISmartCaService> _smartCaServiceMock;

    public GetUserSmartCaStatusQueryHandlerTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserAccountRepository>();
        _smartCaServiceMock = new Mock<ISmartCaService>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserAccount?)null);

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        Func<Task> act = async () => await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenUserHasNoIdentityNumber_ShouldReturnIsEkycVerifiedFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            UserName = "test_user",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = "Nguyễn Văn Chưa eKYC",
                IdentityNumber = string.Empty,
                VerificationStatus = VerificationStatus.PENDING_VERIFICATION
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeFalse();
        result.CitizenId.Should().BeNull();
        result.FullName.Should().Be("Nguyễn Văn Chưa eKYC");
        result.HasSmartCaCertificate.Should().BeFalse();
        result.ActiveCertificate.Should().BeNull();
        _smartCaServiceMock.Verify(s => s.GetUserCertificatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenUserHasIdentityNumberAndActiveCert_ShouldReturnActiveCertDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var citizenId = "001200001234";
        var user = new UserAccount
        {
            Id = userId,
            UserName = "test_user",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = "Nguyễn Văn Đã eKYC",
                IdentityNumber = citizenId,
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var cert = new SmartCaCertificateDto(
            CertId: "cert-01",
            SerialNumber: "54010101493c47d39f8a84b30ed55191",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đang hoạt động",
            CertSubject: $"CN=Nguyễn Văn Đã eKYC,UID=CCCD:{citizenId}",
            ValidFrom: DateTime.UtcNow.AddMonths(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync(citizenId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { cert });

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeTrue();
        result.CitizenId.Should().Be(citizenId);
        result.FullName.Should().Be("Nguyễn Văn Đã eKYC");
        result.HasSmartCaCertificate.Should().BeTrue();
        result.ActiveCertificate.Should().NotBeNull();
        result.ActiveCertificate!.SerialNumber.Should().Be("54010101493c47d39f8a84b30ed55191");
        result.ActiveCertificate.ServiceName.Should().Be("SMARTCA PERSONAL PRO");
        result.ActiveCertificate.CertStatus.Should().Be("Đang hoạt động");
    }

    [Fact]
    public async Task Handle_WhenSmartCaServiceThrowsException_ShouldHandleGracefullyAndReturnHasSmartCaCertificateFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var citizenId = "001200009999";
        var user = new UserAccount
        {
            Id = userId,
            UserName = "test_user",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = "Nguyễn Văn Lỗi Mạng",
                IdentityNumber = citizenId,
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync(citizenId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Gateway timeout"));

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeTrue();
        result.CitizenId.Should().Be(citizenId);
        result.HasSmartCaCertificate.Should().BeFalse();
        result.ActiveCertificate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserCertificatesAreExpiredOrRevoked_ShouldReturnHasSmartCaCertificateFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var citizenId = "001200005555";
        var user = new UserAccount
        {
            Id = userId,
            UserName = "test_expired_cert_user",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = "Hoàng Alpha Hết Hạn",
                IdentityNumber = citizenId,
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var expiredCert = new SmartCaCertificateDto(
            CertId: "cert-expired",
            SerialNumber: "54010101EXPIRED01",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Hết hạn",
            CertSubject: $"CN=Hoàng Alpha Hết Hạn,UID=CCCD:{citizenId}",
            ValidFrom: DateTime.UtcNow.AddYears(-2),
            ValidTo: DateTime.UtcNow.AddDays(-10),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        var revokedCert = new SmartCaCertificateDto(
            CertId: "cert-revoked",
            SerialNumber: "54010101REVOKED01",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Đã thu hồi",
            CertSubject: $"CN=Hoàng Alpha Hết Hạn,UID=CCCD:{citizenId}",
            ValidFrom: DateTime.UtcNow.AddYears(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync(citizenId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { expiredCert, revokedCert });

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeTrue();
        result.CitizenId.Should().Be(citizenId);
        result.HasSmartCaCertificate.Should().BeFalse();
        result.ActiveCertificate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenUserHasMultipleCertificates_ShouldFilterAndPickActiveCertificate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var citizenId = "001200006666";
        var user = new UserAccount
        {
            Id = userId,
            UserName = "test_multi_cert_user",
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = "Trần Phú Quý",
                IdentityNumber = citizenId,
                VerificationStatus = VerificationStatus.VERIFIED
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var expiredCert = new SmartCaCertificateDto(
            CertId: "cert-old",
            SerialNumber: "54010101OLD",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "Hết hạn",
            CertSubject: $"CN=Trần Phú Quý,UID=CCCD:{citizenId}",
            ValidFrom: DateTime.UtcNow.AddYears(-2),
            ValidTo: DateTime.UtcNow.AddDays(-5),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        var activeCert = new SmartCaCertificateDto(
            CertId: "cert-active",
            SerialNumber: "54010101ACTIVE_OK",
            ServiceType: "SMARTCA",
            ServiceName: "SMARTCA PERSONAL PRO",
            CertStatus: "ACTIVE",
            CertSubject: $"CN=Trần Phú Quý,UID=CCCD:{citizenId}",
            ValidFrom: DateTime.UtcNow.AddDays(-1),
            ValidTo: DateTime.UtcNow.AddYears(1),
            CertDataBase64: "MIIF...",
            CaCertBase64: null,
            RootCertBase64: null);

        _smartCaServiceMock.Setup(s => s.GetUserCertificatesAsync(citizenId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SmartCaCertificateDto> { expiredCert, activeCert });

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeTrue();
        result.HasSmartCaCertificate.Should().BeTrue();
        result.ActiveCertificate.Should().NotBeNull();
        result.ActiveCertificate!.SerialNumber.Should().Be("54010101ACTIVE_OK");
        result.ActiveCertificate.CertStatus.Should().Be("ACTIVE");
    }

    [Fact]
    public async Task Handle_WhenUserProfileIsNull_ShouldReturnIsEkycVerifiedFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = new UserAccount
        {
            Id = userId,
            UserName = "user_without_profile",
            UserProfile = null
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new GetUserSmartCaStatusQueryHandler(
            _uowMock.Object,
            _smartCaServiceMock.Object,
            NullLogger<GetUserSmartCaStatusQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetUserSmartCaStatusQuery(userId), CancellationToken.None);

        // Assert
        result.IsEkycVerified.Should().BeFalse();
        result.CitizenId.Should().BeNull();
        result.FullName.Should().BeNull();
        result.HasSmartCaCertificate.Should().BeFalse();
        result.ActiveCertificate.Should().BeNull();
        _smartCaServiceMock.Verify(s => s.GetUserCertificatesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
