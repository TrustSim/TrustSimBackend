using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Chat.DTOs.Requests;

/// <summary>
/// Request gửi tin nhắn mới (văn bản hoặc tệp đính kèm ảnh/PDF) vào cuộc hội thoại.
/// </summary>
public class SendChatMessageRequest
{
    public string? Content { get; set; }
    public IFormFile? AttachmentFile { get; set; }
}
