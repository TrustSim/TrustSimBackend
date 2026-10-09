using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

public class AuctionResultDto
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }

    // Người mua chốt cuối cùng
    public Guid? WinnerId { get; set; }
    public string? WinnerUserName { get; set; }
    public string? WinnerFullName { get; set; }
    public decimal FinalPrice { get; set; }

    // Quán quân ban đầu khi gõ búa (Lưu vết pháp lý nếu Quán quân bùng kèo)
    public Guid? InitialWinnerId { get; set; }
    public string? InitialWinnerUserName { get; set; }
    public string? InitialWinnerFullName { get; set; }
    public decimal? InitialWinningPrice { get; set; }

    // Người về nhì (Á quân)
    public Guid? RunnerUpParticipantId { get; set; }
    public string? RunnerUpUserName { get; set; }
    public string? RunnerUpFullName { get; set; }
    public decimal? RunnerUpFinalPrice { get; set; }

    // Phân loại nguồn chiến thắng
    public AuctionWinType WinType { get; set; } = AuctionWinType.NONE;
    public string WinTypeDescription => WinType switch
    {
        AuctionWinType.ORIGINAL_WINNER => "Quán quân thắng trực tiếp",
        AuctionWinType.RUNNER_UP_TAKEOVER => "Á quân tiếp quản mua lại do Quán quân bùng cọc",
        AuctionWinType.DUTCH_BUY_NOW => "Mua ngay trong phiên Hà Lan",
        _ => "Chưa xác định / Thất bại"
    };

    public decimal? PriceGapRatio { get; set; }
    public decimal? SellerCompensationAmount { get; set; }

    public AuctionResultStatus AuctionResultStatus { get; set; }
    public DateTime? PaymentDeadline { get; set; }
    public bool IsRunnerUpOffered { get; set; }
    public DateTime? RunnerUpPaymentDeadline { get; set; }
    public DateTime? SettledAt { get; set; }
    public DateTime DeterminedAt { get; set; }
    public DateTime ConfirmedAt { get; set; }
    public string? CancelledReason { get; set; }
}

