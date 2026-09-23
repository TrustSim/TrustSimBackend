using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Triển khai thuật toán kiểm toán và xác thực chuỗi khối Hash Chain 5 bước (Ledger Integrity Verifier Engine).
/// </summary>
public class LedgerIntegrityVerifier : ILedgerIntegrityVerifier
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialSigningService _signingService;
    private readonly ILogger<LedgerIntegrityVerifier> _logger;



    public LedgerIntegrityVerifier(
        IUnitOfWork unitOfWork,
        IFinancialSigningService signingService,
        ILogger<LedgerIntegrityVerifier> logger)
    {
        _unitOfWork = unitOfWork;
        _signingService = signingService;
        _logger = logger;
    }

    public async Task<LedgerIntegrityReportDto> VerifyChainAsync(
        long fromSequence = 1,
        int limit = 10000,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var report = new LedgerIntegrityReportDto
        {
            IsChainValid = true,
            IsSignatureValid = true
        };

        var entries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(fromSequence, limit, cancellationToken);
        report.TotalEntriesScanned = entries.Count;

        if (entries.Count == 0)
        {
            sw.Stop();
            report.ExecutionTimeMs = sw.ElapsedMilliseconds;
            return report;
        }

        long expectedSequence = entries.First().SequenceNumber;
        string expectedPrevHash = entries.First().SequenceNumber == 1
            ? LedgerHashHelper.GenesisPrevHash
            : entries.First().PrevHash;

        foreach (var entry in entries)
        {
            // 1. Kiểm tra tính liên tục tuần tự (Sequence Number Monotonicity)
            if (entry.SequenceNumber != expectedSequence)
            {
                report.IsChainValid = false;
                report.Anomalies.Add(new ChainAnomalyDetail
                {
                    SequenceNumber = entry.SequenceNumber,
                    EntryId = entry.Id,
                    AnomalyType = "SEQUENCE_GAP",
                    Description = $"Gián đoạn số thứ tự mắt xích: Kỳ vọng {expectedSequence}, Thực tế {entry.SequenceNumber}.",
                    ExpectedValue = expectedSequence.ToString(),
                    ActualValue = entry.SequenceNumber.ToString()
                });
            }

            // 2. Kiểm tra liên kết mắt xích trước đó (Hash Chain Linkage)
            if (entry.SequenceNumber > 1 && entry.PrevHash != expectedPrevHash)
            {
                report.IsChainValid = false;
                report.Anomalies.Add(new ChainAnomalyDetail
                {
                    SequenceNumber = entry.SequenceNumber,
                    EntryId = entry.Id,
                    AnomalyType = "PREV_HASH_BROKEN",
                    Description = "Mắt xích liên kết PrevHash bị đứt gãy so với EntryHash của bản ghi trước đó.",
                    ExpectedValue = expectedPrevHash,
                    ActualValue = entry.PrevHash
                });
            }

            // 3. Tính toán lại mã băm SHA-256 để kiểm tra can thiệp sửa đổi dữ liệu (Tamper Detection)
            var rawPayload = LedgerHashHelper.BuildEntryPayload(
                entry.SequenceNumber,
                entry.JournalId,
                entry.AccountType,
                entry.EntryType,
                entry.Amount,
                entry.WalletId,
                entry.PrevHash,
                entry.CreatedAt);
            var recomputedHash = LedgerHashHelper.ComputeSha256Hex(rawPayload);

            if (!string.Equals(entry.EntryHash, recomputedHash, StringComparison.OrdinalIgnoreCase))
            {
                report.IsChainValid = false;
                report.Anomalies.Add(new ChainAnomalyDetail
                {
                    SequenceNumber = entry.SequenceNumber,
                    EntryId = entry.Id,
                    AnomalyType = "HASH_MISMATCH",
                    Description = $"Mã băm SHA-256 không khớp! Dữ liệu của bản ghi #{entry.SequenceNumber} đã bị can thiệp trái phép trực tiếp trong DB.",
                    ExpectedValue = recomputedHash,
                    ActualValue = entry.EntryHash
                });
            }

            // 4. Xác thực chữ ký số ECDsa P-256
            var isSignatureValid = await _signingService.VerifySignatureAsync(entry.EntryHash, entry.Signature, cancellationToken);
            if (!isSignatureValid)
            {
                report.IsSignatureValid = false;
                report.Anomalies.Add(new ChainAnomalyDetail
                {
                    SequenceNumber = entry.SequenceNumber,
                    EntryId = entry.Id,
                    AnomalyType = "SIGNATURE_INVALID",
                    Description = $"Chữ ký số ECDsa không hợp lệ hoặc đã bị giả mạo trên mắt xích #{entry.SequenceNumber}.",
                    ExpectedValue = "Hợp lệ theo Master Public Key",
                    ActualValue = "Không hợp lệ / Không khớp Public Key"
                });
            }

            expectedSequence = entry.SequenceNumber + 1;
            expectedPrevHash = entry.EntryHash;
        }

        sw.Stop();
        report.ExecutionTimeMs = sw.ElapsedMilliseconds;

        if (!report.IsChainValid || !report.IsSignatureValid)
        {
            _logger.LogWarning(
                "[Ledger Integrity] PHÁT HIỆN BẤT THƯỜNG! Chuỗi có {AnomalyCount} điểm đứt gãy trong tổng số {Total} mắt xích.",
                report.Anomalies.Count, report.TotalEntriesScanned);
        }

        return report;
    }

    public async Task<LedgerIntegrityReportDto> GenerateFullAuditReportAsync(CancellationToken cancellationToken = default)
    {
        var report = await VerifyChainAsync(1, 50000, cancellationToken);

        // 5. Đối soát số dư toàn sàn giữa Sổ cái kép và Snapshot của các Ví
        var allEntries = await _unitOfWork.LedgerEntries.GetAllOrderedBySequenceAsync(1, 50000, cancellationToken);
        var (wallets, _) = await _unitOfWork.Wallets.GetPagedAsync(pageIndex: 1, pageSize: 1000, cancellationToken: cancellationToken);

        decimal totalPlatformBank1010 = 0;
        decimal totalUserAvailable2010 = 0;
        decimal totalUserLocked2020 = 0;
        decimal totalPlatformEscrow2030 = 0;
        decimal totalPlatformFee4010 = 0;
        decimal totalPlatformPenalty4020 = 0;

        foreach (var entry in allEntries)
        {
            var delta = entry.EntryType == LedgerEntryType.CREDIT ? entry.Amount : -entry.Amount;

            switch (entry.AccountType)
            {
                case FinancialAccountType.EXTERNAL_GATEWAY_CLEARING:
                    // TK 1010 là Tài sản: Debit tăng (+), Credit giảm (-)
                    totalPlatformBank1010 += entry.EntryType == LedgerEntryType.DEBIT ? entry.Amount : -entry.Amount;
                    break;

                case FinancialAccountType.USER_AVAILABLE:
                    totalUserAvailable2010 += delta;
                    break;

                case FinancialAccountType.USER_LOCKED:
                    totalUserLocked2020 += delta;
                    break;

                case FinancialAccountType.PLATFORM_ESCROW:
                    totalPlatformEscrow2030 += delta;
                    break;

                case FinancialAccountType.PLATFORM_FEE_REVENUE:
                    totalPlatformFee4010 += delta;
                    break;

                case FinancialAccountType.PLATFORM_PENALTY_REVENUE:
                    totalPlatformPenalty4020 += delta;
                    break;
            }
        }

        bool hasFrozenWallets = false;

        // Đối soát chi tiết từng ví
        foreach (var wallet in wallets)
        {
            var calculatedAvailable = allEntries
                .Where(e => e.WalletId == wallet.Id && e.AccountType == FinancialAccountType.USER_AVAILABLE)
                .Sum(e => e.EntryType == LedgerEntryType.CREDIT ? e.Amount : -e.Amount);

            var calculatedLocked = allEntries
                .Where(e => e.WalletId == wallet.Id && e.AccountType == FinancialAccountType.USER_LOCKED)
                .Sum(e => e.EntryType == LedgerEntryType.CREDIT ? e.Amount : -e.Amount);

            var availDetail = new AccountReconciliationDetail
            {
                AccountCode = "2010",
                AccountName = $"USER_AVAILABLE (Ví #{wallet.Id.ToString()[..8]})",
                WalletId = wallet.Id,
                LedgerCalculatedBalance = calculatedAvailable,
                SnapshotBalance = wallet.AvailableBalance
            };

            var lockedDetail = new AccountReconciliationDetail
            {
                AccountCode = "2020",
                AccountName = $"USER_LOCKED (Ví #{wallet.Id.ToString()[..8]})",
                WalletId = wallet.Id,
                LedgerCalculatedBalance = calculatedLocked,
                SnapshotBalance = wallet.LockedBalance
            };

            bool isWalletUnbalanced = false;

            // Yêu cầu 1: Chỉ thêm vào mảng Reconciliations các tài khoản vi phạm (Discrepancy != 0)
            if (!availDetail.IsBalanced)
            {
                report.IsBalanceReconciled = false;
                report.Reconciliations.Add(availDetail);
                isWalletUnbalanced = true;
            }

            if (!lockedDetail.IsBalanced)
            {
                report.IsBalanceReconciled = false;
                report.Reconciliations.Add(lockedDetail);
                isWalletUnbalanced = true;
            }

            // Yêu cầu 3: Tự động ĐÓNG BĂNG (FROZEN) ví bị lệch số dư để cô lập rủi ro gian lận
            if (isWalletUnbalanced && wallet.WalletStatus != WalletStatus.FROZEN)
            {
                wallet.WalletStatus = WalletStatus.FROZEN;
                wallet.UpdatedDate = DateTime.UtcNow;
                wallet.UpdatedBy = "SYSTEM_AUDIT_AUTO_FREEZE";
                _unitOfWork.Wallets.Update(wallet);
                hasFrozenWallets = true;

                _logger.LogWarning(
                    "[Ledger Audit] 🚨 PHÁT HIỆN LỆCH SỐ DƯ TÀI KHOẢN! Đã tự động ĐÓNG BĂNG ví '{WalletId}' (Available lệch: {AvailDiff:N0} ₫, Locked lệch: {LockedDiff:N0} ₫).",
                    wallet.Id, availDetail.Discrepancy, lockedDetail.Discrepancy);
            }
        }

        if (hasFrozenWallets)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Kiểm tra phương trình kế toán toàn sàn
        // TK 1010 (Tài sản) == Tổng nợ (2010 + 2020 + 2030) - Doanh thu (4010 + 4020)
        // (hoặc Tài sản = Nợ phải trả + Vốn chủ sở hữu)
        var totalLiabilitiesAndEquity = totalUserAvailable2010 + totalUserLocked2020 + totalPlatformEscrow2030;
        report.IsAccountingEquationBalanced = Math.Abs(totalPlatformBank1010 - (totalLiabilitiesAndEquity - totalPlatformFee4010 - totalPlatformPenalty4020)) < 0.01m;

        return report;
    }


}
