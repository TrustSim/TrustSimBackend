namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// Chi tiết lỗi phát hiện được khi quét chuỗi Hash Chain.
/// </summary>
public class ChainAnomalyDetail
{
    public long SequenceNumber { get; set; }
    public Guid EntryId { get; set; }
    public string AnomalyType { get; set; } = string.Empty; // "HASH_MISMATCH", "PREV_HASH_BROKEN", "SIGNATURE_INVALID", "SEQUENCE_GAP", "DOUBLE_ENTRY_UNBALANCED"
    public string Description { get; set; } = string.Empty;
    public string ExpectedValue { get; set; } = string.Empty;
    public string ActualValue { get; set; } = string.Empty;
}

/// <summary>
/// Chi tiết đối soát số dư của từng tài khoản/ví.
/// </summary>
public class AccountReconciliationDetail
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public Guid? WalletId { get; set; }
    public decimal LedgerCalculatedBalance { get; set; }
    public decimal SnapshotBalance { get; set; }
    public decimal Discrepancy => SnapshotBalance - LedgerCalculatedBalance;
    public bool IsBalanced => Discrepancy == 0;
}

/// <summary>
/// Báo cáo Kiểm toán Toàn vẹn Tài chính &amp; Chuỗi khối Sổ cái (Ledger Integrity Audit Report).
/// </summary>
public class LedgerIntegrityReportDto
{
    /// <summary>
    /// Chuỗi khối Hash Chain hoàn toàn toàn vẹn (Không bị đứt gãy hay giả mạo).
    /// </summary>
    public bool IsChainValid { get; set; } = true;

    /// <summary>
    /// Tất cả chữ ký số ECDsa đều khớp với Public Key của sàn.
    /// </summary>
    public bool IsSignatureValid { get; set; } = true;

    /// <summary>
    /// Số dư sổ cái tính toán khớp 100% với số dư ví tức thời của người dùng.
    /// </summary>
    public bool IsBalanceReconciled { get; set; } = true;

    /// <summary>
    /// Phương trình kế toán kép toàn sàn thỏa mãn: TK 1010 = TK 2010 + TK 2020 + TK 2030 - TK 4010 - TK 4020.
    /// </summary>
    public bool IsAccountingEquationBalanced { get; set; } = true;

    /// <summary>
    /// Tổng số lượng mắt xích đã quét.
    /// </summary>
    public long TotalEntriesScanned { get; set; }

    /// <summary>
    /// Tổng số chứng từ kế toán đã kiểm tra.
    /// </summary>
    public long TotalJournalsScanned { get; set; }

    /// <summary>
    /// Thời điểm thực hiện quét kiểm toán (UTC).
    /// </summary>
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thời gian thực thi quét (mili-giây).
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Danh sách chi tiết các mắt xích bị đứt gãy hoặc bất thường (nếu có).
    /// </summary>
    public List<ChainAnomalyDetail> Anomalies { get; set; } = new();

    /// <summary>
    /// Danh sách đối soát số dư chi tiết giữa Sổ cái và Snapshot ví.
    /// </summary>
    public List<AccountReconciliationDetail> Reconciliations { get; set; } = new();
}
