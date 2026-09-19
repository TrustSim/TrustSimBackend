using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO thể hiện thông tin một dòng bút toán sổ cái bất biến kèm mã băm Hash Chain và Chữ ký số.
/// </summary>
public class LedgerEntryDto
{
    public Guid Id { get; set; }
    public Guid JournalId { get; set; }
    public Guid? WalletId { get; set; }
    public FinancialAccountType AccountType { get; set; }
    public string AccountTypeName => AccountType.ToString();
    public int AccountTypeCode => (int)AccountType;
    public LedgerEntryType EntryType { get; set; }
    public string EntryTypeName => EntryType.ToString();
    public decimal Amount { get; set; }
    public long SequenceNumber { get; set; }
    public string PrevHash { get; set; } = string.Empty;
    public string EntryHash { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }

    public static LedgerEntryDto FromEntity(LedgerEntry entity) => new()
    {
        Id = entity.Id,
        JournalId = entity.JournalId,
        WalletId = entity.WalletId,
        AccountType = entity.AccountType,
        EntryType = entity.EntryType,
        Amount = entity.Amount,
        SequenceNumber = entity.SequenceNumber,
        PrevHash = entity.PrevHash,
        EntryHash = entity.EntryHash,
        Signature = entity.Signature,
        CreatedAt = entity.CreatedAt,
        Description = entity.Description
    };
}
