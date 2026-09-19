using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.Financial;

/// <summary>
/// Mắt xích sổ cái kế toán bất biến (Cryptographic Ledger Entry &amp; Hash Chain Link).
/// Mỗi bản ghi đại diện cho một dòng Nợ hoặc Có, được liên kết chuỗi SHA-256 với mắt xích trước đó
/// và được ký số bằng ECDsa để chống chối bỏ / chống chỉnh sửa trái phép từ DBA.
/// </summary>
public class LedgerEntry : BaseEntity
{
    /// <summary>
    /// ID chứng từ kế toán chứa dòng bút toán này (quan hệ N - 1).
    /// </summary>
    public Guid JournalId { get; set; }

    /// <summary>
    /// ID của ví người dùng liên quan (Null nếu là tài khoản chung của Sàn: 1010, 2030, 4010, 4020, 5010).
    /// </summary>
    public Guid? WalletId { get; set; }

    /// <summary>
    /// Tài khoản kế toán áp dụng (TK 1010, 2010, 2020, 2030, 4010, 4020, 5010).
    /// </summary>
    public FinancialAccountType AccountType { get; set; }

    /// <summary>
    /// Loại bút toán: DEBIT (Nợ) hoặc CREDIT (Có).
    /// </summary>
    public LedgerEntryType EntryType { get; set; }

    /// <summary>
    /// Số tiền phát sinh của dòng bút toán (VNĐ) (luôn > 0).
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Số thứ tự tuần tự toàn cục của mắt xích trong chuỗi khối sổ cái (bắt đầu từ 1, tăng đơn điệu).
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Mã băm SHA-256 của mắt xích ngay trước đó (Genesis Block: chuỗi 64 ký tự '0').
    /// </summary>
    public string PrevHash { get; set; } = string.Empty;

    /// <summary>
    /// Mã băm SHA-256 tính toán từ dữ liệu của dòng này kết hợp với PrevHash:
    /// SHA256(SequenceNumber + JournalId + AccountType + EntryType + Amount + WalletId + PrevHash + CreatedAt).
    /// </summary>
    public string EntryHash { get; set; } = string.Empty;

    /// <summary>
    /// Chữ ký số ECDsa NIST P-256 được tạo bởi HSM / FinancialSigningService trên EntryHash.
    /// </summary>
    public string Signature { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm tạo bản ghi (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Diễn giải chi tiết cho dòng bút toán.
    /// </summary>
    public string? Description { get; set; }

    // --- Navigation Properties ---
    /// <summary>
    /// Chứng từ kế toán sở hữu dòng bút toán này.
    /// </summary>
    public virtual LedgerJournal? Journal { get; set; }

    /// <summary>
    /// Ví tiền của người dùng liên kết (nếu có).
    /// </summary>
    public virtual Wallet? Wallet { get; set; }
}
