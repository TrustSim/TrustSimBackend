namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Request khởi tạo hoặc mở cuộc hội thoại hỗ trợ với Sàn TrustSim.
/// </summary>
public record CreateSupportConversationRequest(
    string? InitialMessage = null);
