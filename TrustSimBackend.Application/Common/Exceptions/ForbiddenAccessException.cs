namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi người dùng không đủ quyền thực hiện hành động (tương ứng HTTP 403).
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("Bạn không có quyền thực hiện thao tác này.")
    {
    }

    public ForbiddenAccessException(string message)
        : base(message)
    {
    }

    public ForbiddenAccessException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
