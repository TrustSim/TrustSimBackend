using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

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

    // Thông tin quy chế bổ sung
    public decimal MinBidStep { get; set; }
    public int MaxStepMultiplier { get; set; } = 10;
    public int MaxSnipeExtensionsPerParticipant { get; set; } = 3;
    public int PaymentGracePeriodHours { get; set; } = 24;
    public decimal DepositAmount { get; set; }
    public float ExtensionDurationSeconds { get; set; }
    public decimal DutchDecreaseAmount { get; set; }
    public float DutchDecreaseIntervalSeconds { get; set; }

    // Thông tin SIM bổ sung
    public string? FormatRhythmName { get; set; }
    public List<string> Categories { get; set; } = new();

    // Trạng thái của người dùng hiện tại
    public bool IsUserRegistered { get; set; }
}
