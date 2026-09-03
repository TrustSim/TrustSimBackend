using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs;

public class AuctionDto
{
    public Guid Id { get; set; }
    public string AuctionCode { get; set; } = string.Empty;
    public AuctionType AuctionType { get; set; }
    public AuctionStatus AuctionStatus { get; set; }
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal ReservePrice { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public DateTime RegistrationStartAt { get; set; }
    public DateTime RegistrationEndAt { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public Guid SimNumberId { get; set; }
    public string SimPhoneNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public int TotalParticipants { get; set; }
    public int TotalBids { get; set; }
}

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

public record CreateAuctionRequest(
    Guid SimNumberId,
    AuctionType AuctionType,
    decimal StartingPrice,
    decimal ReservePrice,
    DateTime StartAt,
    DateTime EndAt,
    DateTime RegistrationStartAt,
    DateTime RegistrationEndAt
);

public record PlaceBidRequest(
    decimal BidAmount
);
