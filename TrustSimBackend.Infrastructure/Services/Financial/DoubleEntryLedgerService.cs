using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Triển khai dịch vụ Sổ cái Kế toán Ghi kép Bất biến (Double-Entry Ledger Engine) &amp; Nối chuỗi Hash Chain SHA-256.
/// </summary>
public class DoubleEntryLedgerService : ILedgerService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialSigningService _signingService;
    private readonly ILogger<DoubleEntryLedgerService> _logger;

    private static readonly SemaphoreSlim _chainLock = new(1, 1);

    public DoubleEntryLedgerService(
        IUnitOfWork unitOfWork,
        IFinancialSigningService signingService,
        ILogger<DoubleEntryLedgerService> logger)
    {
        _unitOfWork = unitOfWork;
        _signingService = signingService;
        _logger = logger;
    }

    public async Task<LedgerJournal> PostJournalAsync(
        LedgerTransactionType transactionType,
        string description,
        IReadOnlyList<LedgerEntryParam> entries,
        string? referenceType = null,
        Guid? referenceId = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count < 2)
        {
            throw new InvalidOperationException("Chứng từ kế toán ghi kép phải có ít nhất 2 dòng bút toán (Nợ và Có).");
        }

        var totalDebit = entries.Where(e => e.EntryType == LedgerEntryType.DEBIT).Sum(e => e.Amount);
        var totalCredit = entries.Where(e => e.EntryType == LedgerEntryType.CREDIT).Sum(e => e.Amount);

        if (totalDebit != totalCredit || totalDebit <= 0)
        {
            throw new InvalidOperationException(
                $"[VI PHẠM NGUYÊN TẮC KẾ TOÁN] Chứng từ không cân bằng: Tổng Nợ ({totalDebit:N2}) != Tổng Có ({totalCredit:N2}).");
        }

        // Đảm bảo tuần tự hóa mắt xích Hash Chain một cách an toàn và chống race-condition
        await _chainLock.WaitAsync(cancellationToken);
        try
        {
            var journalId = Guid.NewGuid();
            var journalCode = $"JRN-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(100000, 999999)}";
            var now = new DateTime((DateTime.UtcNow.Ticks / 10) * 10, DateTimeKind.Utc);

            var journal = new LedgerJournal
            {
                Id = journalId,
                JournalCode = journalCode,
                TransactionType = transactionType,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                IdempotencyKey = idempotencyKey,
                Description = description,
                TotalAmount = totalDebit,
                Status = JournalStatus.POSTED,
                PostedAt = now
            };

            // Lấy mắt xích gần nhất để nối chuỗi
            var lastEntry = await _unitOfWork.LedgerEntries.GetLastEntryAsync(cancellationToken);
            var currentSequence = lastEntry?.SequenceNumber ?? 0;
            var currentPrevHash = lastEntry?.EntryHash ?? LedgerHashHelper.GenesisPrevHash;

            var createdEntries = new List<LedgerEntry>();

            foreach (var param in entries)
            {
                currentSequence++;
                var entryId = Guid.NewGuid();
                var createdAt = now;

                // Tính toán chuỗi băm dữ liệu bản ghi bằng Helper chuẩn hóa
                var rawPayload = LedgerHashHelper.BuildEntryPayload(
                    currentSequence,
                    journalId,
                    param.AccountType,
                    param.EntryType,
                    param.Amount,
                    param.WalletId,
                    currentPrevHash,
                    createdAt);
                var entryHash = LedgerHashHelper.ComputeSha256Hex(rawPayload);

                // Ký số ECDsa P-256 trên mã băm
                var signature = await _signingService.SignHashAsync(entryHash, cancellationToken);

                var entry = new LedgerEntry
                {
                    Id = entryId,
                    JournalId = journalId,
                    WalletId = param.WalletId,
                    AccountType = param.AccountType,
                    EntryType = param.EntryType,
                    Amount = param.Amount,
                    SequenceNumber = currentSequence,
                    PrevHash = currentPrevHash,
                    EntryHash = entryHash,
                    Signature = signature,
                    CreatedAt = createdAt,
                    Description = param.Description ?? description
                };

                createdEntries.Add(entry);
                journal.Entries.Add(entry);

                // Cập nhật số dư tức thời (Materialized Balance) của ví nếu liên quan đến tài khoản người dùng
                if (param.WalletId.HasValue)
                {
                    await UpdateMaterializedWalletBalanceAsync(param.WalletId.Value, param.AccountType, param.EntryType, param.Amount, cancellationToken);
                }

                currentPrevHash = entryHash;
            }

            // Ký số toàn bộ chứng từ
            var journalPayload = $"{journal.JournalCode}:{journal.TransactionType}:{journal.TotalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}:{journal.PostedAt.ToUniversalTime():yyyy-MM-ddTHH:mm:ss.ffffffZ}:{journal.Entries.Count}";
            var journalHash = LedgerHashHelper.ComputeSha256Hex(journalPayload);
            journal.SignedHash = await _signingService.SignHashAsync(journalHash, cancellationToken);

            await _unitOfWork.LedgerJournals.AddAsync(journal, cancellationToken);
            await _unitOfWork.LedgerEntries.AddRangeAsync(createdEntries, cancellationToken);

            _logger.LogInformation(
                "[Double-Entry Ledger] Ghi sổ THÀNH CÔNG chứng từ '{JournalCode}' (Type: {Type}, Total: {Amount:N0} VNĐ, Entries: {Count}, Sequence: {SeqFrom} -> {SeqTo})",
                journal.JournalCode, journal.TransactionType, journal.TotalAmount, createdEntries.Count, createdEntries.First().SequenceNumber, createdEntries.Last().SequenceNumber);

            return journal;
        }
        finally
        {
            _chainLock.Release();
        }
    }

    private async Task UpdateMaterializedWalletBalanceAsync(
        Guid walletId,
        FinancialAccountType accountType,
        LedgerEntryType entryType,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var wallet = await _unitOfWork.Wallets.GetByIdAsync(walletId, cancellationToken);
        if (wallet == null) return;

        switch (accountType)
        {
            case FinancialAccountType.USER_AVAILABLE:
                if (entryType == LedgerEntryType.CREDIT)
                {
                    wallet.AvailableBalance += amount; // Sàn nợ User thêm -> Tăng khả dụng
                }
                else if (entryType == LedgerEntryType.DEBIT)
                {
                    wallet.AvailableBalance -= amount; // Giảm nợ User -> Giảm khả dụng
                }
                break;

            case FinancialAccountType.USER_LOCKED:
                if (entryType == LedgerEntryType.CREDIT)
                {
                    wallet.LockedBalance += amount; // Tăng tiền phong tỏa
                }
                else if (entryType == LedgerEntryType.DEBIT)
                {
                    wallet.LockedBalance -= amount; // Giải phóng tiền phong tỏa
                }
                break;
        }

        wallet.LockVersion++;
        _unitOfWork.Wallets.Update(wallet);
    }

    public async Task<LedgerJournal> PostDepositAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? externalId,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.EXTERNAL_GATEWAY_CLEARING, LedgerEntryType.DEBIT, amount, null, $"Nạp tiền qua SePay GD #{externalId}"),
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.CREDIT, amount, walletId, $"Cộng tiền khả dụng mã {transactionCode}")
        };

        return await PostJournalAsync(
            LedgerTransactionType.TOP_UP,
            $"Nạp tiền SePay vào ví {transactionCode}",
            entries,
            "WalletTransaction",
            null,
            idempotencyKey,
            cancellationToken);
    }

    public async Task<LedgerJournal> PostWithdrawLockAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.DEBIT, amount, walletId, $"Khóa tiền yêu cầu rút {transactionCode}"),
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.CREDIT, amount, walletId, $"Phong tỏa chờ Payout ngân hàng {transactionCode}")
        };

        return await PostJournalAsync(
            LedgerTransactionType.WITHDRAW_LOCK,
            $"Khóa tiền rút {transactionCode}",
            entries,
            "WalletTransaction",
            null,
            idempotencyKey,
            cancellationToken);
    }

    public async Task<LedgerJournal> PostWithdrawCompleteAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string? externalId = null,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.DEBIT, amount, walletId, $"Giải phóng phong tỏa rút tiền {transactionCode}"),
            new(FinancialAccountType.EXTERNAL_GATEWAY_CLEARING, LedgerEntryType.CREDIT, amount, null, $"Chi tiền thực tế qua Bank Payout GD #{externalId}")
        };

        return await PostJournalAsync(
            LedgerTransactionType.WITHDRAW_COMPLETED,
            $"Chi trả rút tiền thành công {transactionCode}",
            entries,
            "WalletTransaction",
            null,
            $"WD-COMPLETE-{transactionCode}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostWithdrawRefundAsync(
        Guid walletId,
        decimal amount,
        string transactionCode,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.DEBIT, amount, walletId, $"Giải phóng phong tỏa do rút thất bại: {reason}"),
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.CREDIT, amount, walletId, $"Hoàn tiền về ví khả dụng mã {transactionCode}")
        };

        return await PostJournalAsync(
            LedgerTransactionType.WITHDRAW_REFUND,
            $"Hoàn tiền rút thất bại {transactionCode}",
            entries,
            "WalletTransaction",
            null,
            $"WD-REFUND-{transactionCode}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostAuctionDepositLockAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.DEBIT, amount, walletId, "Khấu trừ khả dụng để đặt cọc đấu giá"),
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.CREDIT, amount, walletId, "Khóa tiền cọc tham gia phòng đấu giá")
        };

        return await PostJournalAsync(
            LedgerTransactionType.AUCTION_DEPOSIT_LOCK,
            $"Khóa cọc tham gia đấu giá #{auctionDepositId}",
            entries,
            "AuctionDeposit",
            auctionDepositId,
            $"AUC-DEP-LOCK-{auctionDepositId}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostAuctionDepositReleaseAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.DEBIT, amount, walletId, "Giải phóng tiền cọc không trúng đấu giá"),
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.CREDIT, amount, walletId, "Hoàn trả tiền cọc về ví khả dụng")
        };

        return await PostJournalAsync(
            LedgerTransactionType.AUCTION_DEPOSIT_RELEASED,
            $"Hoàn cọc đấu giá #{auctionDepositId}",
            entries,
            "AuctionDeposit",
            auctionDepositId,
            $"AUC-DEP-REL-{auctionDepositId}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostAuctionDepositForfeitedAsync(
        Guid walletId,
        decimal amount,
        Guid auctionDepositId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.USER_LOCKED, LedgerEntryType.DEBIT, amount, walletId, "Tịch thu tiền cọc do vi phạm quy chế đấu giá"),
            new(FinancialAccountType.PLATFORM_PENALTY_REVENUE, LedgerEntryType.CREDIT, amount, null, "Doanh thu tiền phạt vi phạm quy chế")
        };

        return await PostJournalAsync(
            LedgerTransactionType.AUCTION_DEPOSIT_FORFEITED,
            $"Tịch thu cọc vi phạm đấu giá #{auctionDepositId}",
            entries,
            "AuctionDeposit",
            auctionDepositId,
            $"AUC-DEP-FORFEIT-{auctionDepositId}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostEscrowHoldAsync(
        Guid buyerWalletId,
        decimal totalEscrowAmount,
        decimal existingDepositAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default)
    {
        var additionalAmount = totalEscrowAmount - existingDepositAmount;
        var entries = new List<LedgerEntryParam>();

        if (existingDepositAmount > 0)
        {
            entries.Add(new(FinancialAccountType.USER_LOCKED, LedgerEntryType.DEBIT, existingDepositAmount, buyerWalletId, "Chuyển tiền cọc đấu giá vào quỹ Escrow"));
        }

        if (additionalAmount > 0)
        {
            entries.Add(new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.DEBIT, additionalAmount, buyerWalletId, "Khấu trừ số dư khả dụng bổ sung vào Escrow"));
        }

        entries.Add(new(FinancialAccountType.PLATFORM_ESCROW, LedgerEntryType.CREDIT, totalEscrowAmount, null, "Ký quỹ Escrow bảo đảm chuyển nhượng SIM"));

        return await PostJournalAsync(
            LedgerTransactionType.ESCROW_HOLD,
            $"Ký quỹ Escrow bảo đảm giao dịch #{escrowId}",
            entries,
            "Escrow",
            escrowId,
            $"ESCROW-HOLD-{escrowId}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostEscrowReleaseAsync(
        Guid sellerWalletId,
        decimal totalEscrowAmount,
        decimal platformFeeAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default)
    {
        var netPayout = totalEscrowAmount - platformFeeAmount;
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.PLATFORM_ESCROW, LedgerEntryType.DEBIT, totalEscrowAmount, null, "Giải phóng quỹ ký quỹ Escrow sau chuyển nhượng SIM"),
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.CREDIT, netPayout, sellerWalletId, $"Thanh toán tiền bán SIM cho người bán (sau phí sàn)"),
            new(FinancialAccountType.PLATFORM_FEE_REVENUE, LedgerEntryType.CREDIT, platformFeeAmount, null, $"Doanh thu phí dịch vụ sàn TrustSim ({platformFeeAmount:N0} VNĐ)")
        };

        return await PostJournalAsync(
            LedgerTransactionType.ESCROW_RELEASE,
            $"Giải ngân Escrow #{escrowId} cho người bán",
            entries,
            "Escrow",
            escrowId,
            $"ESCROW-REL-{escrowId}",
            cancellationToken);
    }

    public async Task<LedgerJournal> PostEscrowRefundAsync(
        Guid buyerWalletId,
        decimal totalEscrowAmount,
        Guid escrowId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<LedgerEntryParam>
        {
            new(FinancialAccountType.PLATFORM_ESCROW, LedgerEntryType.DEBIT, totalEscrowAmount, null, "Giải phóng quỹ Escrow do giao dịch hủy"),
            new(FinancialAccountType.USER_AVAILABLE, LedgerEntryType.CREDIT, totalEscrowAmount, buyerWalletId, "Hoàn trả 100% tiền ký quỹ Escrow về ví người mua")
        };

        return await PostJournalAsync(
            LedgerTransactionType.ESCROW_REFUND,
            $"Hoàn trả tiền ký quỹ Escrow #{escrowId}",
            entries,
            "Escrow",
            escrowId,
            $"ESCROW-REFUND-{escrowId}",
            cancellationToken);
    }


}
