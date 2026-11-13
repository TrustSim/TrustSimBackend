namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Loại bằng chứng chuyển nhượng SIM.
/// </summary>
public enum TransferProofType
{
    /// <summary>Biên bản bàn giao/chuyển quyền sử dụng từ điểm giao dịch nhà mạng do Người Bán nộp.</summary>
    SELLER_HANDOVER,

    /// <summary>Ảnh chụp kiểm tra sóng/app nhà mạng/tin nhắn TTTB do Người Mua nghiệm thu nộp.</summary>
    BUYER_RECEIPT
}
