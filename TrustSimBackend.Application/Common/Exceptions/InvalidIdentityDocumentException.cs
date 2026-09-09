namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi hình ảnh giấy tờ tùy thân không thể nhận diện hoặc quá mờ.
/// </summary>
public class InvalidIdentityDocumentException : Exception
{
    public InvalidIdentityDocumentException(string details)
        : base($"Giấy tờ tùy thân không hợp lệ hoặc không thể nhận diện: {details}") { }
}
