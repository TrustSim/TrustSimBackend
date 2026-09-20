using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Cung cấp thuật toán chuẩn hóa và tính toán mã băm SHA-256 cho chuỗi mắt xích Sổ cái Kế toán (Ledger Hash Chain).
/// </summary>
public static class LedgerHashHelper
{
    public const string GenesisPrevHash = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>
    /// Xây dựng chuỗi raw payload bất biến đại diện cho một bản ghi bút toán (LedgerEntry).
    /// Chuẩn hóa định dạng thời gian và số thực độc lập với hệ điều hành và cơ sở dữ liệu.
    /// </summary>
    public static string BuildEntryPayload(
        long sequenceNumber,
        Guid journalId,
        FinancialAccountType accountType,
        LedgerEntryType entryType,
        decimal amount,
        Guid? walletId,
        string prevHash,
        DateTime createdAt)
    {
        // Truncate/format timestamp to microsecond precision (PostgreSQL timestamptz limit: 6 decimal places)
        var utcTime = createdAt.ToUniversalTime();
        var formattedTime = utcTime.ToString("yyyy-MM-ddTHH:mm:ss.ffffffZ", CultureInfo.InvariantCulture);
        var formattedAmount = amount.ToString("F2", CultureInfo.InvariantCulture);
        var walletStr = walletId.HasValue ? walletId.Value.ToString() : string.Empty;

        return $"{sequenceNumber}:{journalId}:{accountType}:{entryType}:{formattedAmount}:{walletStr}:{prevHash}:{formattedTime}";
    }

    /// <summary>
    /// Tính toán mã băm SHA-256 dạng Hex viết thường (Lowercase Hex String).
    /// </summary>
    public static string ComputeSha256Hex(string input)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
