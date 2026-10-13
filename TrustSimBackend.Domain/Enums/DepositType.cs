namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Phân loại mục đích của khoản tiền ký quỹ trong phiên đấu giá.
/// </summary>
public enum DepositType
{
    /// <summary>
    /// Tiền cọc ban đầu nộp khi đăng ký tham gia phòng đấu giá.
    /// </summary>
    INITIAL = 1,

    /// <summary>
    /// Tiền cọc bổ sung lũy tiến (Progressive Margin) được trích thu từ ví người thắng 
    /// ngay sau khi phiên kết thúc nếu chưa thanh toán đủ 100% giá trị SIM.
    /// </summary>
    PROGRESSIVE_ADDITIONAL = 2
}
