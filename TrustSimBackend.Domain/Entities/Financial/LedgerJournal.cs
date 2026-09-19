using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial;

/// <summary>
/// Chứng từ kế toán ghi kép (Ledger Journal Header).
/// Đại diện cho một giao dịch kinh tế hoàn chỉnh, đảm bảo Tổng Nợ = Tổng Có.
/// Bất biến (Append-Only) — Không được sửa đổi hoặc xóa khi đã POSTED.
/// </summary>
public class LedgerJournal : AuditableEntity
{
    /// <summary>
    /// Mã chứng từ kế toán duy nhất (VD: "JRN-20260919-8923").
    /// </summary>
    public string JournalCode { get; set; } = string.Empty;

    /// <summary>
    /// Loại nghiệp vụ kế toán phát sinh.
    /// </summary>
    public LedgerTransactionType TransactionType { get; set; }

    /// <summary>
    /// Loại thực thể tham chiếu gốc (VD: "PaymentIntent", "AuctionDeposit", "Escrow", "WalletTransaction").
    /// </summary>
    public string? ReferenceType { get; set; }

    /// <summary>
    /// ID của thực thể tham chiếu gốc.
    /// </summary>
    public Guid? ReferenceId { get; set; }

    /// <summary>
    /// Khóa chống trùng lặp nghiệp vụ (Idempotency Key).
    /// </summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Diễn giải chi tiết nội dung chứng từ kế toán.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Tổng giá trị giao dịch của chứng từ (VNĐ) (Tổng Nợ = Tổng Có = TotalAmount).
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Trạng thái chứng từ: POSTED (đã ghi sổ), REVERSED (đã đảo).
    /// </summary>
    public JournalStatus Status { get; set; } = JournalStatus.POSTED;

    /// <summary>
    /// Chữ ký số ECDsa xác thực tính toàn vẹn của chứng từ.
    /// </summary>
    public string? SignedHash { get; set; }

    /// <summary>
    /// Thời điểm ghi nhận chứng từ vào sổ cái.
    /// </summary>
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;

    // --- Navigation Properties ---
    /// <summary>
    /// Danh sách các dòng bút toán Nợ / Có thuộc chứng từ này (quan hệ 1 - N).
    /// </summary>
    public virtual ICollection<LedgerEntry> Entries { get; set; } = new HashSet<LedgerEntry>();
}
