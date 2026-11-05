namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Request mở hoặc lấy cuộc hội thoại 1-1 với một người dùng đã xác thực danh tính khác.
/// </summary>
public record CreateDirectConversationRequest(
    Guid TargetUserId,
    string? InitialMessage = null);
