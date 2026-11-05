namespace TrustSimBackend.Application.Common.Exceptions;

/// <summary>
/// Ngoại lệ được ném ra khi tệp PDF tải lên bị lỗi cấu trúc, dùng bảng mã cũ (VNI/TCVN3)
/// hoặc không nhúng font chữ gây lỗi hiển thị tiếng Việt trên thiết bị khác.
/// </summary>
public class InvalidPdfFontException : Exception
{
    public InvalidPdfFontException(string message) : base(message)
    {
    }

    public InvalidPdfFontException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
