namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Phân loại cuộc hội thoại trên hệ thống TrustSim.
/// </summary>
public enum ConversationType
{
    /// <summary>Hội thoại trực tiếp 1-1 giữa 2 người dùng đã xác thực danh tính (eKYC).</summary>
    DIRECT = 0,

    /// <summary>Hội thoại hỗ trợ giữa người dùng và Nhân viên Sàn / Admin.</summary>
    SUPPORT = 1
}
