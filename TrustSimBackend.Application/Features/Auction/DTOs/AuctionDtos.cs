using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs;

/// <summary>
/// Bộ quy tắc của phiên đấu giá (dùng cho cả Create, Update, và Display).
/// </summary>
public class AuctionRuleDto
{
    // --- Quy tắc đặt giá ---
    public decimal MinimumBidIncrement { get; set; }
    public decimal DepositAmount { get; set; }
    public int MaximumBidCountPerParticipant { get; set; } = 100;
    public int MaximumBidCountPerMinute { get; set; } = 10;
    public float MinimumBidIntervalSeconds { get; set; } = 5f;

    // --- Anti-Sniping ---
    public float ExtensionThresholdSeconds { get; set; } = 60f;
    public float ExtensionDurationSeconds { get; set; } = 60f;
    public int MaximumExtensionCount { get; set; } = 5;

    // --- Dutch Auction ---
    public decimal DutchDecreaseAmount { get; set; }
    public float DutchDecreaseIntervalSeconds { get; set; }
}

/// <summary>
/// Tóm tắt 1 phiên đấu giá trong danh sách phân trang.
/// </summary>
public class AuctionListItemDto
{
    public Guid AuctionId { get; set; }
    public string AuctionCode { get; set; } = string.Empty;
    public AuctionType AuctionType { get; set; }
    public string AuctionTypeName => AuctionType.ToString();
    public AuctionStatus AuctionStatus { get; set; }
    public string AuctionStatusName => AuctionStatus.ToString();

    // SIM info
    public Guid SimNumberId { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? CarrierName { get; set; }
    public string? FormatRhythmName { get; set; }

    // Giá
    public decimal StartingPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal ReservePrice { get; set; }

    // Thời gian
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public DateTime RegistrationStartAt { get; set; }
    public DateTime RegistrationEndAt { get; set; }

    // Author (chỉ cho Admin)
    public Guid AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorEmail { get; set; }
}

/// <summary>
/// Chi tiết đầy đủ 1 phiên đấu giá.
/// </summary>
public class AuctionDetailDto : AuctionListItemDto
{
    // SIM categories
    public List<string> Categories { get; set; } = new();

    // Bộ quy tắc
    public AuctionRuleDto? Rule { get; set; }

    // Metadata
    public DateTime CreatedDate { get; set; }
    public DateTime? UpdatedDate { get; set; }
}
