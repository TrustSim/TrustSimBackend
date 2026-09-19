using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ Kiểm toán &amp; So khớp Toàn vẹn Chuỗi khối Sổ cái (Ledger Integrity Verifier Engine).
/// </summary>
public interface ILedgerIntegrityVerifier
{
    /// <summary>
    /// Quét tuần tự toàn bộ mắt xích từ Genesis Block hoặc từ một Sequence cụ thể
    /// để kiểm tra tính liên tục, tính toàn vẹn SHA-256 Hash Chain và chữ ký số ECDsa.
    /// </summary>
    Task<LedgerIntegrityReportDto> VerifyChainAsync(
        long fromSequence = 1,
        int limit = 10000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Đối soát số dư luỹ kế tính toán từ Sổ cái kép với Materialized Balance tức thời của tất cả các ví.
    /// </summary>
    Task<LedgerIntegrityReportDto> GenerateFullAuditReportAsync(CancellationToken cancellationToken = default);
}
