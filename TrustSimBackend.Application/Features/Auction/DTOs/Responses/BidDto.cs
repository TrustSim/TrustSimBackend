using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

public class BidDto
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid ParticipantId { get; set; }
    public Guid UserAccountId { get; set; }
    public string UserNameMasked { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public DateTime PlacedAt { get; set; }
    public BidStatus Status { get; set; }
}
