using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Tham số cho một dòng bút toán trong chứng từ kế toán.
/// </summary>
public record LedgerEntryParam(
    FinancialAccountType AccountType,
    LedgerEntryType EntryType,
    decimal Amount,
    Guid? WalletId = null,
    string? Description = null);

/// <summary>
/// Dịch vụ Sổ cái Kế toán Ghi kép Bất biến (Append-Only Double-Entry Ledger Engine).
/// Chịu trách nhiệm ghi nhận các chứng từ kế toán, đảm bảo Tổng Nợ = Tổng Có,
/// sinh mã băm SHA-256 nối chuỗi Hash Chain và ký số ECDsa trong 1 Transaction nguyên tử.
/// </summary>
public interface ILedgerService
{
    /// <summary>
    /// Ghi nhận chứng từ kế toán kép tổng quát.
    /// </summary>
    Task<LedgerJournal> PostJournalAsync(
        LedgerTransactionType transactionType,
        string description,
        IReadOnlyList<LedgerEntryParam> entries,
        string? referenceType = null,
        Guid? referenceId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.1: Nạp tiền qua Cổng thanh toán (Nợ 1010 / Có 2010).
    /// </summary>
    Task<LedgerJournal> PostDepositAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? externalId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.2 (Pha 1): Khóa tiền yêu cầu rút về ngân hàng (Nợ 2010 / Có 2020).
    /// </summary>
    Task<LedgerJournal> PostWithdrawLockAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.2 (Pha 2A): Chi trả rút tiền thành công qua ngân hàng (Nợ 2020 / Có 1010).
    /// </summary>
    Task<LedgerJournal> PostWithdrawCompleteAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? externalId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.2 (Pha 2B): Hoàn tiền tự động khi Payout thất bại (Nợ 2020 / Có 2010).
    /// </summary>
    Task<LedgerJournal> PostWithdrawRefundAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.3: Đặt cọc tham gia đấu giá SIM (Nợ 2010 / Có 2020).
    /// </summary>
    Task<LedgerJournal> PostAuctionDepositLockAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.3 (KQ_A): Hoàn cọc khi không trúng đấu giá (Nợ 2020 / Có 2010).
    /// </summary>
    Task<LedgerJournal> PostAuctionDepositReleaseAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.3 (KQ_B): Tịch thu cọc khi vi phạm quy chế (Nợ 2020 / Có 4020).
    /// </summary>
    Task<LedgerJournal> PostAuctionDepositForfeitedAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.4: Ký quỹ Escrow sau khi thắng đấu giá (Nợ 2020 / Có 2030).
    /// </summary>
    Task<LedgerJournal> PostEscrowHoldAsync(
        Guid buyerWalletId,
        decimal totalEscrowAmount,
        decimal existingDepositAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.5 (Thành công): Giải ngân Escrow cho Người bán &amp; thu phí sàn (Nợ 2030 / Có 2010 Seller + Có 4010 Sàn).
    /// </summary>
    Task<LedgerJournal> PostEscrowReleaseAsync(
        Guid sellerWalletId,
        decimal totalEscrowAmount,
        decimal platformFeeAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Nghiệp vụ 3.5.5 (Hủy): Hoàn Escrow về Người mua (Nợ 2030 / Có 2010 Buyer).
    /// </summary>
    Task<LedgerJournal> PostEscrowRefundAsync(
        Guid buyerWalletId,
        decimal totalEscrowAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default);
}
