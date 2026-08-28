namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Chuẩn hóa cấu trúc dữ liệu JSON trả về cho mọi API.
/// Giúp Frontend (Web/Mobile) dễ dàng bắt kết quả thành công/thất bại thống nhất.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của payload trả về</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public ApiResponse() { }

    // Phương thức tĩnh tiện lợi khi trả về thành công có dữ liệu
    public static ApiResponse<T> SuccessResult(T data, string message = "Thao tác thành công.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    // Phương thức tĩnh tiện lợi khi trả về thành công không kèm dữ liệu
    public static ApiResponse<T> SuccessResult(string message = "Thao tác thành công.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = default
        };
    }

    // Phương thức tĩnh tiện lợi khi trả về lỗi đơn lẻ
    public static ApiResponse<T> FailureResult(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors ?? new List<string>()
        };
    }
}
