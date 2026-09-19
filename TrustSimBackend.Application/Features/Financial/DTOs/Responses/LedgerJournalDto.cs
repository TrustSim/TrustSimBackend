using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Responses;

/// <summary>
/// DTO thể hiện thông tin chứng từ kế toán ghi kép kèm danh sách các dòng bút toán Nợ/Có.
/// </summary>
public class LedgerJournalDto
{
    public Guid Id { get; set; }
    public string JournalCode { get; set; } = string.Empty;
    public LedgerTransactionType TransactionType { get; set; }
    public string TransactionTypeName => TransactionType.ToString();
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public JournalStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public string? SignedHash { get; set; }
    public DateTime PostedAt { get; set; }
    public List<LedgerEntryDto> Entries { get; set; } = new();

    public static LedgerJournalDto FromEntity(LedgerJournal entity) => new()
    {
        Id = entity.Id,
        JournalCode = entity.JournalCode,
        TransactionType = entity.TransactionType,
        ReferenceType = entity.ReferenceType,
        ReferenceId = entity.ReferenceId,
        IdempotencyKey = entity.IdempotencyKey,
        Description = entity.Description,
        TotalAmount = entity.TotalAmount,
        Status = entity.Status,
        SignedHash = entity.SignedHash,
        PostedAt = entity.PostedAt,
        Entries = entity.Entries?.Select(LedgerEntryDto.FromEntity).ToList() ?? new()
    };
}
