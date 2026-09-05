using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Dispute.DTOs.Responses;

public class DisputeTicketDto
{
    public Guid Id { get; set; }
    public Guid TradeTransactionId { get; set; }
    public Guid ComplainantId { get; set; }
    public string ComplainantName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; }
    public Guid? ResolvedByAdminId { get; set; }
    public string? AdminResolutionNotes { get; set; }
    public DateTime CreatedDate { get; set; }
}
