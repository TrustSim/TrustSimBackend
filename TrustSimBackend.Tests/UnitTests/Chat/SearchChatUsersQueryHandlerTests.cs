using FluentAssertions;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.Queries;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Chat;

public class SearchChatUsersQueryHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserAccountRepository> _userRepoMock;
    private readonly Mock<IChatConversationRepository> _chatConvRepoMock;
    private readonly SearchVerifiedUsersForChatQueryHandler _sut;

    public SearchChatUsersQueryHandlerTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserAccountRepository>();
        _chatConvRepoMock = new Mock<IChatConversationRepository>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.ChatConversations).Returns(_chatConvRepoMock.Object);

        _sut = new SearchVerifiedUsersForChatQueryHandler(_uowMock.Object);
    }

    [Theory]
    [InlineData(VerificationStatus.PENDING_VERIFICATION)]
    [InlineData(VerificationStatus.REJECTED)]
    [InlineData(VerificationStatus.EXPIRED)]
    public async Task Handle_WhenCallerIsNotEkycVerified_ShouldThrowUserProfileNotVerifiedException(VerificationStatus status)
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var unverifiedCaller = new UserAccount
        {
            Id = callerId,
            UserName = "user_unverified",
            IsActive = true,
            Role = UserRole.USER,
            UserProfile = new UserProfile
            {
                UserAccountId = callerId,
                VerificationStatus = status
            }
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unverifiedCaller);

        var query = new SearchVerifiedUsersForChatQuery(
            Keyword: "0988888888",
            CurrentUserId: callerId,
            CurrentUserRole: UserRole.USER);

        // Act
        Func<Task> act = async () => await _sut.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserProfileNotVerifiedException>();
        _chatConvRepoMock.Verify(
            r => r.SearchVerifiedUsersByEmailOrPhoneAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenCallerIsInactive_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var callerId = Guid.NewGuid();
        var lockedCaller = new UserAccount
        {
            Id = callerId,
            UserName = "locked_user",
            IsActive = false,
            Role = UserRole.USER
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(callerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lockedCaller);

        var query = new SearchVerifiedUsersForChatQuery(
            Keyword: "test@trustsim.vn",
            CurrentUserId: callerId,
            CurrentUserRole: UserRole.USER);

        // Act
        Func<Task> act = async () => await _sut.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>();
    }

    [Fact]
    public async Task SearchVerifiedUsersByEmailOrPhoneAsync_WhenSearchingByEmail_ShouldReturnOnlyVerifiedUsersExcludingSelf()
    {
        // Arrange
        using var dbContext = TestTrustSimDbContext.CreateInMemory();
        var repository = new ChatConversationRepository(dbContext);

        var callerId = Guid.NewGuid();
        var verifiedTargetId = Guid.NewGuid();
        var unverifiedTargetId = Guid.NewGuid();

        var caller = CreateTestUser(callerId, "caller_user", "caller.vip@trustsim.vn", "0901111111", "Người Gọi", VerificationStatus.VERIFIED);
        var verifiedTarget = CreateTestUser(verifiedTargetId, "target_verified", "buyer.vip@trustsim.vn", "0902222222", "Nguyễn Văn Đã Duyệt", VerificationStatus.VERIFIED);
        var unverifiedTarget = CreateTestUser(unverifiedTargetId, "target_unverified", "buyer.unverified@trustsim.vn", "0903333333", "Trần Chưa Duyệt", VerificationStatus.PENDING_VERIFICATION);

        dbContext.UserAccounts.AddRange(caller, verifiedTarget, unverifiedTarget);
        await dbContext.SaveChangesAsync();

        // Act
        var results = await repository.SearchVerifiedUsersByEmailOrPhoneAsync("buyer", callerId, limit: 10);

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(verifiedTargetId);
        results[0].UserProfile!.FullName.Should().Be("Nguyễn Văn Đã Duyệt");
    }

    [Fact]
    public async Task SearchVerifiedUsersByEmailOrPhoneAsync_WhenSearchingByPhoneNumberInUserProfile_ShouldReturnMatchingVerifiedUser()
    {
        // Arrange
        using var dbContext = TestTrustSimDbContext.CreateInMemory();
        var repository = new ChatConversationRepository(dbContext);

        var callerId = Guid.NewGuid();
        var verifiedByPhoneId = Guid.NewGuid();
        var unverifiedSamePrefixId = Guid.NewGuid();

        var caller = CreateTestUser(callerId, "caller_user", "caller@trustsim.vn", "0911111111", "Người Tìm Kiếm", VerificationStatus.VERIFIED);
        var verifiedByPhone = CreateTestUser(verifiedByPhoneId, "sim_tycoon", "tycoon@trustsim.vn", "0988999888", "Lê Đại Gia SIM", VerificationStatus.VERIFIED);
        var unverifiedSamePrefix = CreateTestUser(unverifiedSamePrefixId, "clone_user", "clone@trustsim.vn", "0988999777", "Tài Khoản Chưa KYC", VerificationStatus.PENDING_VERIFICATION);

        dbContext.UserAccounts.AddRange(caller, verifiedByPhone, unverifiedSamePrefix);
        await dbContext.SaveChangesAsync();

        // Act
        var results = await repository.SearchVerifiedUsersByEmailOrPhoneAsync("0988999", callerId, limit: 10);

        // Assert
        results.Should().HaveCount(1);
        results[0].Id.Should().Be(verifiedByPhoneId);
        results[0].UserProfile!.PhoneNumber.Should().Be("0988999888");
    }

    private static UserAccount CreateTestUser(
        Guid userId,
        string userName,
        string email,
        string phoneNumber,
        string fullName,
        VerificationStatus verificationStatus)
    {
        var profileId = Guid.NewGuid();
        return new UserAccount
        {
            Id = userId,
            UserName = userName,
            Email = email,
            IsActive = true,
            IsDeleted = false,
            Role = UserRole.USER,
            UserProfile = new UserProfile
            {
                Id = profileId,
                UserAccountId = userId,
                FullName = fullName,
                Email = email,
                PhoneNumber = phoneNumber,
                VerificationStatus = verificationStatus,
                IsDeleted = false
            }
        };
    }
}
