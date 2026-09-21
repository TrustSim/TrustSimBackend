namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// DTO gửi lên từ Admin khi đóng băng hoặc mở đóng băng ví.
/// </summary>
public class AdminFreezeWalletRequest
{
    /// <summary>
    /// true: Đóng băng ví, false: Mở đóng băng ví.
    /// </summary>
    public bool Freeze { get; set; } = true;
}
