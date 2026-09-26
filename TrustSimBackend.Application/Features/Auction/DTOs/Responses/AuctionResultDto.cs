using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

public class AuctionResultDto
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid? WinnerId { get; set; }
    public string? WinnerUserName { get; set; }
    public decimal FinalPrice { get; set; }
    public Guid? RunnerUpParticipantId { get; set; }
    public string? RunnerUpUserName { get; set; }
    public decimal? RunnerUpFinalPrice { get; set; }
    public AuctionResultStatus AuctionResultStatus { get; set; }
    public DateTime? PaymentDeadline { get; set; }
    public bool IsRunnerUpOffered { get; set; }
    public DateTime? RunnerUpPaymentDeadline { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime DeterminedAt { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string? CancelledReason { get; set; }
}
