using FluentAssertions;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Chat.Commands;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Application.Features.Chat.Queries;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Repositories;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Chat;

public class SupportQueueAndPrivacyIsolationTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserAccountRepository> _userRepoMock;
    private readonly Mock<IChatConversationRepository> _chatConvRepoMock;
    private readonly Mock<IChatMessageRepository> _chatMsgRepoMock;
    private readonly Mock<IChatRealtimeService> _realtimeMock;
    private readonly Mock<ICloudStorageService> _cloudStorageMock;
    private readonly Mock<IPdfFontValidatorService> _pdfValidatorMock;

    public SupportQueueAndPrivacyIsolationTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserAccountRepository>();
        _chatConvRepoMock = new Mock<IChatConversationRepository>();
        _chatMsgRepoMock = new Mock<IChatMessageRepository>();
        _realtimeMock = new Mock<IChatRealtimeService>();
        _cloudStorageMock = new Mock<ICloudStorageService>();
        _pdfValidatorMock = new Mock<IPdfFontValidatorService>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.ChatConversations).Returns(_chatConvRepoMock.Object);
        _uowMock.Setup(u => u.ChatMessages).Returns(_chatMsgRepoMock.Object);
    }

    [Fact]
    public async Task GetOrCreateSupportConversation_WhenVerifiedUserCreatesSupportChat_ShouldSetPendingStatusAndNotifyQueue()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var customer = CreateAccount(customerId, "customer_vip", "Khách Hàng VIP", UserRole.USER, VerificationStatus.VERIFIED);

        _userRepoMock.Setup(r => r.GetByIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(customer);
        _chatConvRepoMock.Setup(r => r.GetActiveSupportConversationByUserIdAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatConversation?)null);

        var handler = new GetOrCreateSupportConversationCommandHandler(_uowMock.Object, _realtimeMock.Object);
        var command = new GetOrCreateSupportConversationCommand(
            CurrentUserId: customerId,
            CurrentUserRole: UserRole.USER,
            InitialMessage: "Nhờ sàn tư vấn sang tên SIM số đẹp");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Type.Should().Be(ConversationType.SUPPORT);
        result.SupportStatus.Should().Be(SupportConversationStatus.PENDING);
        result.AssignedStaff.Should().BeNull();
        result.IsPrivateSupport.Should().BeFalse();

        _chatConvRepoMock.Verify(r => r.AddAsync(It.Is<ChatConversation>(c =>
            c.Type == ConversationType.SUPPORT &&
            c.SupportStatus == SupportConversationStatus.PENDING &&
            c.AssignedStaffId == null), It.IsAny<CancellationToken>()), Times.Once);

        _realtimeMock.Verify(r => r.NotifyNewPendingSupportConversationAsync(
            It.IsAny<ChatConversationSummaryDto>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserConversations_WhenSupportConversationIsPending_ShouldBeVisibleToAllStaffAndAdmins()
    {
        // Arrange
        using var dbContext = TestTrustSimDbContext.CreateInMemory();
        var convRepo = new ChatConversationRepository(dbContext);
        _uowMock.Setup(u => u.ChatConversations).Returns(convRepo);

        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var staffBId = Guid.NewGuid();
        var adminAId = Guid.NewGuid();

        var customer = CreateAccount(customerId, "customer_1", "Khách Hàng Cần Hỗ Trợ", UserRole.USER, VerificationStatus.VERIFIED);
        var staffA = CreateAccount(staffAId, "staff_a", "Chuyên Viên A", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var staffB = CreateAccount(staffBId, "staff_b", "Chuyên Viên B", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var adminA = CreateAccount(adminAId, "admin_a", "Quản Trị Viên A", UserRole.ADMIN, VerificationStatus.VERIFIED);

        var pendingSupportConv = new ChatConversation
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            InitiatorUser = customer,
            SupportStatus = SupportConversationStatus.PENDING,
            AssignedStaffId = null,
            LastMessagePreview = "Tôi cần hỗ trợ",
            LastMessageAt = DateTime.UtcNow
        };

        dbContext.UserAccounts.AddRange(customer, staffA, staffB, adminA);
        dbContext.ChatConversations.Add(pendingSupportConv);
        await dbContext.SaveChangesAsync();

        var queryHandler = new GetUserConversationsQueryHandler(_uowMock.Object);

        // Act
        var staffAResult = await queryHandler.Handle(new GetUserConversationsQuery(staffAId, UserRole.CONSULTANT), CancellationToken.None);
        var staffBResult = await queryHandler.Handle(new GetUserConversationsQuery(staffBId, UserRole.CONSULTANT), CancellationToken.None);
        var adminAResult = await queryHandler.Handle(new GetUserConversationsQuery(adminAId, UserRole.ADMIN), CancellationToken.None);

        // Assert
        staffAResult.Items.Should().ContainSingle(c => c.Id == pendingSupportConv.Id && c.CanClaim);
        staffBResult.Items.Should().ContainSingle(c => c.Id == pendingSupportConv.Id && c.CanClaim);
        adminAResult.Items.Should().ContainSingle(c => c.Id == pendingSupportConv.Id && c.CanClaim);
    }

    [Fact]
    public async Task ClaimSupportConversation_WhenStaffAClaimsPendingConversation_ShouldAssignStaffCreateSystemMessageAndHideFromOthers()
    {
        // Arrange
        using var dbContext = TestTrustSimDbContext.CreateInMemory();
        var convRepo = new ChatConversationRepository(dbContext);
        var msgRepo = new ChatMessageRepository(dbContext);
        var userRepo = new UserAccountRepository(dbContext);

        _uowMock.Setup(u => u.Users).Returns(userRepo);
        _uowMock.Setup(u => u.ChatConversations).Returns(convRepo);
        _uowMock.Setup(u => u.ChatMessages).Returns(msgRepo);
        _uowMock.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken ct) => dbContext.SaveChangesAsync(ct));
        _uowMock.Setup(u => u.ClearChangeTracker())
            .Callback(() => dbContext.ChangeTracker.Clear());

        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var staffBId = Guid.NewGuid();
        var adminBId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var customer = CreateAccount(customerId, "cust_1", "Khách Hàng A", UserRole.USER, VerificationStatus.VERIFIED);
        var staffA = CreateAccount(staffAId, "staff_a", "Chuyên Viên A", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var staffB = CreateAccount(staffBId, "staff_b", "Chuyên Viên B", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var adminB = CreateAccount(adminBId, "admin_b", "Quản Trị Viên B", UserRole.ADMIN, VerificationStatus.VERIFIED);

        var supportConv = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            InitiatorUser = customer,
            SupportStatus = SupportConversationStatus.PENDING,
            AssignedStaffId = null,
            CreatedDate = DateTime.UtcNow
        };

        dbContext.UserAccounts.AddRange(customer, staffA, staffB, adminB);
        dbContext.ChatConversations.Add(supportConv);
        await dbContext.SaveChangesAsync();

        var claimHandler = new ClaimSupportConversationCommandHandler(_uowMock.Object, _realtimeMock.Object);
        var listHandler = new GetUserConversationsQueryHandler(_uowMock.Object);

        // Act
        var claimedDto = await claimHandler.Handle(
            new ClaimSupportConversationCommand(convId, staffAId, UserRole.CONSULTANT),
            CancellationToken.None);

        var customerList = await listHandler.Handle(new GetUserConversationsQuery(customerId, UserRole.USER), CancellationToken.None);
        var staffAList = await listHandler.Handle(new GetUserConversationsQuery(staffAId, UserRole.CONSULTANT), CancellationToken.None);
        var staffBList = await listHandler.Handle(new GetUserConversationsQuery(staffBId, UserRole.CONSULTANT), CancellationToken.None);
        var adminBList = await listHandler.Handle(new GetUserConversationsQuery(adminBId, UserRole.ADMIN), CancellationToken.None);

        // Assert
        claimedDto.SupportStatus.Should().Be(SupportConversationStatus.CLAIMED);
        claimedDto.AssignedStaff.Should().NotBeNull();
        claimedDto.AssignedStaff!.UserId.Should().Be(staffAId);
        claimedDto.IsPrivateSupport.Should().BeTrue();
        claimedDto.CanClaim.Should().BeFalse();

        // Khách hàng và Nhân viên A nhìn thấy cuộc hội thoại riêng tư
        customerList.Items.Should().ContainSingle(c => c.Id == convId);
        staffAList.Items.Should().ContainSingle(c => c.Id == convId);

        // Nhân viên B và cả Admin B đều bị ẩn hoàn toàn khỏi danh sách!
        staffBList.Items.Should().BeEmpty();
        adminBList.Items.Should().BeEmpty();

        _realtimeMock.Verify(r => r.NotifySupportConversationClaimedAsync(
            convId,
            customerId,
            staffAId,
            "Chuyên Viên A",
            It.IsAny<DateTime>(),
            It.IsAny<ChatConversationSummaryDto>(),
            It.IsAny<ChatConversationSummaryDto>(),
            It.Is<ChatMessageDto>(m => m.MessageType == ChatMessageType.SYSTEM),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(UserRole.CONSULTANT)]
    [InlineData(UserRole.ADMIN)]
    public async Task ClaimSupportConversation_WhenAlreadyClaimedByStaffA_AndAnotherStaffOrAdminAttemptsToClaim_ShouldThrowForbiddenAccessException(
        UserRole intruderRole)
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var intruderUser = CreateAccount(intruderId, "intruder_staff", "Nhân Sự Khác", intruderRole, VerificationStatus.VERIFIED);
        var claimedConversation = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            AssignedStaffId = staffAId,
            SupportStatus = SupportConversationStatus.CLAIMED,
            ClaimedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(intruderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intruderUser);
        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedConversation);

        var sut = new ClaimSupportConversationCommandHandler(_uowMock.Object, _realtimeMock.Object);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new ClaimSupportConversationCommand(convId, intruderId, intruderRole),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*riêng tư*");
        _chatConvRepoMock.Verify(r => r.TryClaimSupportConversationAtomicallyAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserRole.CONSULTANT)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetConversationMessages_WhenSupportConversationIsClaimedByStaffA_AndAnotherStaffOrAdminAttemptsToRead_ShouldThrowForbiddenAccessException(
        UserRole intruderRole)
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var claimedConversation = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            AssignedStaffId = staffAId,
            SupportStatus = SupportConversationStatus.CLAIMED
        };

        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedConversation);

        var sut = new GetConversationMessagesQueryHandler(_uowMock.Object);
        var query = new GetConversationMessagesQuery(
            ConversationId: convId,
            CurrentUserId: intruderId,
            CurrentUserRole: intruderRole);

        // Act
        Func<Task> act = async () => await sut.Handle(query, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*riêng tư*");
        _chatMsgRepoMock.Verify(r => r.GetMessagesByConversationPagedAsync(
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(UserRole.CONSULTANT)]
    [InlineData(UserRole.ADMIN)]
    public async Task SendChatMessage_WhenSupportConversationIsClaimedByStaffA_AndAnotherStaffOrAdminAttemptsToSendMessage_ShouldThrowForbiddenAccessException(
        UserRole intruderRole)
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var intruderId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var intruderUser = CreateAccount(intruderId, "intruder", "Người Can Thiệp", intruderRole, VerificationStatus.VERIFIED);
        var claimedConversation = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            AssignedStaffId = staffAId,
            SupportStatus = SupportConversationStatus.CLAIMED
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(intruderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(intruderUser);
        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimedConversation);

        var sut = new SendChatMessageCommandHandler(
            _uowMock.Object,
            _cloudStorageMock.Object,
            _pdfValidatorMock.Object,
            _realtimeMock.Object);

        var command = new SendChatMessageCommand(
            ConversationId: convId,
            SenderId: intruderId,
            SenderRole: intruderRole,
            Content: "Tin nhắn can thiệp trái phép");

        // Act
        Func<Task> act = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*riêng tư*");
        _chatMsgRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChatMessage_WhenSupportConversationIsPendingUnclaimed_AndStaffAttemptsToSendWithoutClaimingFirst_ShouldThrowBadRequestException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var staffAId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var staffA = CreateAccount(staffAId, "staff_a", "Chuyên Viên A", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var pendingConversation = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            AssignedStaffId = null,
            SupportStatus = SupportConversationStatus.PENDING
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(staffAId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffA);
        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingConversation);

        var sut = new SendChatMessageCommandHandler(
            _uowMock.Object,
            _cloudStorageMock.Object,
            _pdfValidatorMock.Object,
            _realtimeMock.Object);

        var command = new SendChatMessageCommand(
            ConversationId: convId,
            SenderId: staffAId,
            SenderRole: UserRole.CONSULTANT,
            Content: "Trả lời khi chưa bấm Duyệt");

        // Act
        Func<Task> act = async () => await sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Chấp nhận / Duyệt*");
        _chatMsgRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ClaimSupportConversation_WhenAtomicClaimFailsDueToRaceCondition_ShouldThrowForbiddenAccessException()
    {
        // Arrange
        var customerId = Guid.NewGuid();
        var staffBId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var staffB = CreateAccount(staffBId, "staff_b", "Chuyên Viên B", UserRole.CONSULTANT, VerificationStatus.VERIFIED);
        var pendingSnapshot = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.SUPPORT,
            InitiatorUserId = customerId,
            AssignedStaffId = null,
            SupportStatus = SupportConversationStatus.PENDING
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(staffBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(staffB);
        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingSnapshot);

        // Giả lập Race Condition: Staff A vừa kịp duyệt ở mức DB ngay trước mili-giây Staff B gọi TryClaimSupportConversationAtomicallyAsync
        _chatConvRepoMock.Setup(r => r.TryClaimSupportConversationAtomicallyAsync(
                convId, staffBId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var sut = new ClaimSupportConversationCommandHandler(_uowMock.Object, _realtimeMock.Object);

        // Act
        Func<Task> act = async () => await sut.Handle(
            new ClaimSupportConversationCommand(convId, staffBId, UserRole.CONSULTANT),
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenAccessException>()
            .WithMessage("*vừa được một nhân viên hoặc quản trị viên khác tiếp nhận*");
        _chatMsgRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserAccount CreateAccount(
        Guid userId,
        string userName,
        string fullName,
        UserRole role,
        VerificationStatus verificationStatus)
    {
        return new UserAccount
        {
            Id = userId,
            UserName = userName,
            Email = $"{userName}@trustsim.vn",
            IsActive = true,
            IsDeleted = false,
            Role = role,
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = fullName,
                Email = $"{userName}@trustsim.vn",
                PhoneNumber = "0909888777",
                VerificationStatus = verificationStatus,
                IsDeleted = false
            }
        };
    }
}
