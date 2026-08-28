namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ bắn ra khi không tìm thấy bản ghi dữ liệu trong Database (tương ứng HTTP 404).
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException()
        : base("Không tìm thấy tài nguyên yêu cầu.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Không tìm thấy đối tượng '{name}' với khóa '{key}'.")
    {
    }
}
