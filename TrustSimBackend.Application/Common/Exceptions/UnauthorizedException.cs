namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ xảy ra khi người dùng chưa đăng nhập hoặc token không hợp lệ (HTTP 401).
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException()
        : base("Yêu cầu xác thực không thành công hoặc Token đã hết hạn.")
    {
    }

    public UnauthorizedException(string message)
        : base(message)
    {
    }

    public UnauthorizedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
