namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Phân loại các hình thức gửi tin nhắn SMS của SpeedSMS Gateway.
/// </summary>
public enum SpeedSmsType
{
    /// <summary>Tin nhắn quảng cáo</summary>
    QC = 1,

    /// <summary>Tin nhắn chăm sóc khách hàng qua đầu số cố định</summary>
    CSKH = 2,

    /// <summary>Tin nhắn sử dụng Brandname đăng ký riêng</summary>
    BrandName = 3,

    /// <summary>Tin nhắn sử dụng Brandname Notify mặc định</summary>
    BrandNameNotify = 4,

    /// <summary>Tin nhắn gửi qua ứng dụng SpeedSMS Gateway cài đặt trên thiết bị Android cá nhân</summary>
    Gateway = 5
}
