namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại nội dung tin nhắn trong cuộc hội thoại.
/// </summary>
public enum ChatMessageType
{
    TEXT = 0,
    IMAGE = 1,
    PDF_DOCUMENT = 2,
    SYSTEM = 3
}
