using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Chat.Commands;
using TrustSimBackend.Application.Features.Chat.DTOs.Requests;
using TrustSimBackend.Application.Features.Chat.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Chat;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Services;
using TrustSimBackend.Tests.Common;
using Xunit;

namespace TrustSimBackend.Tests.UnitTests.Chat;

public class DirectConversationCommandTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserAccountRepository> _userRepoMock;
    private readonly Mock<IChatConversationRepository> _chatConvRepoMock;
    private readonly Mock<IChatMessageRepository> _chatMsgRepoMock;
    private readonly Mock<IChatRealtimeService> _realtimeMock;
    private readonly GetOrCreateDirectConversationCommandHandler _sut;

    public DirectConversationCommandTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserAccountRepository>();
        _chatConvRepoMock = new Mock<IChatConversationRepository>();
        _chatMsgRepoMock = new Mock<IChatMessageRepository>();
        _realtimeMock = new Mock<IChatRealtimeService>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);
        _uowMock.Setup(u => u.ChatConversations).Returns(_chatConvRepoMock.Object);
        _uowMock.Setup(u => u.ChatMessages).Returns(_chatMsgRepoMock.Object);

        _sut = new GetOrCreateDirectConversationCommandHandler(_uowMock.Object, _realtimeMock.Object);
    }

    [Fact]
    public async Task Handle_WhenTargetIsSelf_ShouldThrowBadRequestException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new GetOrCreateDirectConversationCommand(
            CurrentUserId: userId,
            CurrentUserRole: UserRole.USER,
            TargetUserId: userId);

        // Act
        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*chính mình*");
    }

    [Fact]
    public async Task Handle_WhenInitiatorUserIsNotEkycVerified_ShouldThrowUserProfileNotVerifiedException()
    {
        // Arrange
        var initiatorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var unverifiedInitiator = CreateUser(initiatorId, "user_a", "Người A", VerificationStatus.PENDING_VERIFICATION);
        _userRepoMock.Setup(r => r.GetByIdAsync(initiatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unverifiedInitiator);

        var command = new GetOrCreateDirectConversationCommand(
            CurrentUserId: initiatorId,
            CurrentUserRole: UserRole.USER,
            TargetUserId: targetId);

        // Act
        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserProfileNotVerifiedException>();
        _chatConvRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTargetUserIsNotEkycVerified_ShouldThrowBadRequestException()
    {
        // Arrange
        var initiatorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        var verifiedInitiator = CreateUser(initiatorId, "user_a", "Người A Đã KYC", VerificationStatus.VERIFIED);
        var unverifiedTarget = CreateUser(targetId, "user_b", "Người B Chưa KYC", VerificationStatus.REJECTED);

        _userRepoMock.Setup(r => r.GetByIdAsync(initiatorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(verifiedInitiator);
        _userRepoMock.Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unverifiedTarget);

        var command = new GetOrCreateDirectConversationCommand(
            CurrentUserId: initiatorId,
            CurrentUserRole: UserRole.USER,
            TargetUserId: targetId);

        // Act
        Func<Task> act = async () => await _sut.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*xác thực danh tính (eKYC)*");
        _chatConvRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDirectConversationAlreadyExists_ShouldReuseExistingConversation()
    {
        // Arrange
        var userAId = Guid.NewGuid();
        var userBId = Guid.NewGuid();
        var existingConvId = Guid.NewGuid();

        var userA = CreateUser(userAId, "user_a", "Nguyễn Văn A", VerificationStatus.VERIFIED);
        var userB = CreateUser(userBId, "user_b", "Trần Thị B", VerificationStatus.VERIFIED);

        var existingConv = new ChatConversation
        {
            Id = existingConvId,
            Type = ConversationType.DIRECT,
            InitiatorUserId = userBId,
            ParticipantUserId = userAId,
            InitiatorUser = userB,
            ParticipantUser = userA,
            SupportStatus = SupportConversationStatus.NONE
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(userAId, It.IsAny<CancellationToken>())).ReturnsAsync(userA);
        _userRepoMock.Setup(r => r.GetByIdAsync(userBId, It.IsAny<CancellationToken>())).ReturnsAsync(userB);
        _chatConvRepoMock.Setup(r => r.GetDirectConversationBetweenUsersAsync(userAId, userBId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingConv);

        var command = new GetOrCreateDirectConversationCommand(
            CurrentUserId: userAId,
            CurrentUserRole: UserRole.USER,
            TargetUserId: userBId,
            InitialMessage: null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.Id.Should().Be(existingConvId);
        result.Type.Should().Be(ConversationType.DIRECT);
        result.DisplayTitle.Should().Be("Trần Thị B");
        _chatConvRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendChatMessage_WhenDirectChatHasPdfAttachmentWithUnembeddedFont_ShouldThrowInvalidPdfFontException()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var sender = CreateUser(senderId, "sender_vip", "Người Gửi", VerificationStatus.VERIFIED);
        var receiver = CreateUser(receiverId, "receiver_vip", "Người Nhận", VerificationStatus.VERIFIED);

        var conversation = new ChatConversation
        {
            Id = convId,
            Type = ConversationType.DIRECT,
            InitiatorUserId = senderId,
            ParticipantUserId = receiverId,
            InitiatorUser = sender,
            ParticipantUser = receiver,
            SupportStatus = SupportConversationStatus.NONE
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(senderId, It.IsAny<CancellationToken>())).ReturnsAsync(sender);
        _chatConvRepoMock.Setup(r => r.GetByIdWithDetailsAsync(convId, It.IsAny<CancellationToken>())).ReturnsAsync(conversation);

        var cloudStorageMock = new Mock<ICloudStorageService>();
        var pdfLoggerMock = new Mock<ILogger<PdfFontValidatorService>>();
        var pdfValidator = new PdfFontValidatorService(pdfLoggerMock.Object);

        var sendHandler = new SendChatMessageCommandHandler(
            _uowMock.Object,
            cloudStorageMock.Object,
            pdfValidator,
            _realtimeMock.Object);

        var badPdfBytes = PdfTestSampleBuilder.CreateUnembeddedStandardFontPdfBytes("Hop dong SIM loi font");
        var pdfStream = new MemoryStream(badPdfBytes);
        var formFile = new FormFile(pdfStream, 0, badPdfBytes.Length, "AttachmentFile", "dinh_kem_loi_font.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var command = new SendChatMessageCommand(
            ConversationId: convId,
            SenderId: senderId,
            SenderRole: UserRole.USER,
            Content: "Gửi file hợp đồng",
            AttachmentFile: formFile);

        // Act
        Func<Task> act = async () => await sendHandler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidPdfFontException>()
            .WithMessage("*dinh_kem_loi_font.pdf*");
        _chatMsgRepoMock.Verify(r => r.AddAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static UserAccount CreateUser(
        Guid userId,
        string userName,
        string fullName,
        VerificationStatus status)
    {
        return new UserAccount
        {
            Id = userId,
            UserName = userName,
            Email = $"{userName}@trustsim.vn",
            IsActive = true,
            IsDeleted = false,
            Role = UserRole.USER,
            UserProfile = new UserProfile
            {
                Id = Guid.NewGuid(),
                UserAccountId = userId,
                FullName = fullName,
                Email = $"{userName}@trustsim.vn",
                PhoneNumber = "0909123456",
                VerificationStatus = status
            }
        };
    }
}
