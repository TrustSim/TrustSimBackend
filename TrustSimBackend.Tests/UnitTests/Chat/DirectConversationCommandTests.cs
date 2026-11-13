using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
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

    [Fact]
    public async Task SendChatMessage_WhenValidPngImageAttached_ShouldUploadToChatImagesBucketAndReturnImageMessageWith7DayExpiration()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var sender = CreateUser(senderId, "sender_img", "Người Gửi Ảnh", VerificationStatus.VERIFIED);
        var receiver = CreateUser(receiverId, "receiver_img", "Người Nhận Ảnh", VerificationStatus.VERIFIED);

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

        var expectedImageUrl = $"http://localhost:9000/chat-images/conversations/{convId:N}/sim_photo.png";
        var cloudStorageMock = new Mock<ICloudStorageService>();
        cloudStorageMock.Setup(s => s.UploadChatImageAsync(It.IsAny<IFormFile>(), convId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedImageUrl);

        var pdfValidatorMock = new Mock<IPdfFontValidatorService>();
        var minioOptions = Options.Create(new MinioSettings
        {
            ChatImageBucket = "chat-images",
            ChatImageExpirationDays = 7
        });

        var sendHandler = new SendChatMessageCommandHandler(
            _uowMock.Object,
            cloudStorageMock.Object,
            pdfValidatorMock.Object,
            _realtimeMock.Object,
            minioOptions);

        // Chuẩn Magic Bytes PNG: 89 50 4E 47 0D 0A 1A 0A
        var validPngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };
        var pngStream = new MemoryStream(validPngBytes);
        var formFile = new FormFile(pngStream, 0, validPngBytes.Length, "AttachmentFile", "anh_sim_vip.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var command = new SendChatMessageCommand(
            ConversationId: convId,
            SenderId: senderId,
            SenderRole: UserRole.USER,
            Content: null,
            AttachmentFile: formFile);

        // Act
        var result = await sendHandler.Handle(command, CancellationToken.None);

        // Assert
        result.MessageType.Should().Be(ChatMessageType.IMAGE);
        result.AttachmentUrl.Should().Be(expectedImageUrl);
        result.AttachmentFileName.Should().Be("anh_sim_vip.png");
        result.Content.Should().Be("[Hình ảnh] anh_sim_vip.png");
        result.IsAttachmentExpired.Should().BeFalse();
        result.AttachmentExpiresAt.Should().NotBeNull();
        result.AttachmentExpiresAt!.Value.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromMinutes(1));

        cloudStorageMock.Verify(s => s.UploadChatImageAsync(It.IsAny<IFormFile>(), convId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendChatMessage_WhenImageFileHasSpoofedMagicBytes_ShouldThrowBadRequestExceptionAndNeverUploadToMinio()
    {
        // Arrange
        var senderId = Guid.NewGuid();
        var receiverId = Guid.NewGuid();
        var convId = Guid.NewGuid();

        var sender = CreateUser(senderId, "sender_spoof", "Người Gửi", VerificationStatus.VERIFIED);
        var receiver = CreateUser(receiverId, "receiver_spoof", "Người Nhận", VerificationStatus.VERIFIED);

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
        var pdfValidatorMock = new Mock<IPdfFontValidatorService>();

        var sendHandler = new SendChatMessageCommandHandler(
            _uowMock.Object,
            cloudStorageMock.Object,
            pdfValidatorMock.Object,
            _realtimeMock.Object);

        // Nội dung giả mạo (MZ executable / HTML script) nhưng đặt tên đuôi .png
        var spoofedBytes = "MZ_FAKE_EXECUTABLE_SCRIPT_PAYLOAD"u8.ToArray();
        var stream = new MemoryStream(spoofedBytes);
        var spoofedFile = new FormFile(stream, 0, spoofedBytes.Length, "AttachmentFile", "malware_disguised.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var command = new SendChatMessageCommand(
            ConversationId: convId,
            SenderId: senderId,
            SenderRole: UserRole.USER,
            Content: "Ảnh giả mạo",
            AttachmentFile: spoofedFile);

        // Act
        Func<Task> act = async () => await sendHandler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BadRequestException>()
            .WithMessage("*Magic Bytes*");
        cloudStorageMock.Verify(s => s.UploadChatImageAsync(It.IsAny<IFormFile>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
