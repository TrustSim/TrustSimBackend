namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả trả về từ API gửi tin nhắn của SpeedSMS.
/// </summary>
public class SpeedSmsSendResponse
{
    /// <summary>Trạng thái thực thi ("success" hoặc "error").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Mã phản hồi từ SpeedSMS (ví dụ: "00", "0", 200).</summary>
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public object? Code { get; set; }

    /// <summary>Thông điệp chi tiết mô tả kết quả hoặc lỗi.</summary>
    public string? Message { get; set; }

    /// <summary>Dữ liệu chi tiết về chi phí và số lượng tin nhắn.</summary>
    public SpeedSmsData? Data { get; set; }

    /// <summary>Kiểm tra yêu cầu gửi SMS có thành công hay không.</summary>
    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Chi tiết dữ liệu SMS đã gửi.
/// </summary>
public class SpeedSmsData
{
    /// <summary>Tổng số tiền phí đã trừ cho đợt gửi.</summary>
    public double TotalMoney { get; set; }

    /// <summary>Tổng số lượng tin nhắn SMS đã gửi đi.</summary>
    public int TotalSms { get; set; }

    /// <summary>Danh sách các số điện thoại không hợp lệ nếu có.</summary>
    public List<string>? InvalidPhone { get; set; }
}

/// <summary>
/// Kết quả trả về từ API tra cứu thông tin tài khoản SpeedSMS.
/// </summary>
public class SpeedSmsUserInfoResponse
{
    /// <summary>Trạng thái kết quả ("success" hoặc "error").</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Mã phản hồi (ví dụ "00").</summary>
    [System.Text.Json.Serialization.JsonPropertyName("code")]
    public object? Code { get; set; }

    /// <summary>Thông điệp lỗi nếu có.</summary>
    public string? Message { get; set; }

    /// <summary>Dữ liệu tài khoản người dùng và số dư.</summary>
    public SpeedSmsUserData? Data { get; set; }

    /// <summary>Kiểm tra xem yêu cầu tra cứu có thành công hay không.</summary>
    public bool IsSuccess => string.Equals(Status, "success", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Thông tin chi tiết số dư và tài khoản SpeedSMS.
/// </summary>
public class SpeedSmsUserData
{
    /// <summary>Địa chỉ email đăng ký tài khoản SpeedSMS.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Số dư tài khoản hiện tại (VND).</summary>
    public double Balance { get; set; }

    /// <summary>Đơn vị tiền tệ (mặc định VND).</summary>
    public string Currency { get; set; } = "VND";
}
