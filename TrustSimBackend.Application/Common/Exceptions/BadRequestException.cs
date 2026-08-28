namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi nghiệp vụ không thể thực hiện do dữ liệu không hợp lệ hoặc vi phạm quy tắc (tương ứng HTTP 400).
/// </summary>
public class BadRequestException : Exception
{
    public BadRequestException()
        : base("Yêu cầu không hợp lệ.")
    {
    }

    public BadRequestException(string message)
        : base(message)
    {
    }

    public BadRequestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
