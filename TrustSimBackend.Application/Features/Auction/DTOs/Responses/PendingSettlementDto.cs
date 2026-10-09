using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Responses;

/// <summary>
/// DTO chứa thông tin các phiên đấu giá đang cần người dùng xử lý quyết định hoặc thanh toán (Pending Settlement Action).
/// </summary>
public class PendingSettlementDto
{
    public Guid AuctionId { get; set; }
    public string AuctionCode { get; set; } = string.Empty;
    public string SimPhoneNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public AuctionType AuctionType { get; set; }
    public AuctionStatus AuctionStatus { get; set; }
    public AuctionResultStatus AuctionResultStatus { get; set; }

    /// <summary>
    /// Vai trò của người dùng trong phiên: WINNER, RUNNER_UP, SELLER
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Hành động cần thực hiện: PAY_SETTLEMENT, DECIDE_RUNNER_UP, RESPOND_RUNNER_UP_OFFER
    /// </summary>
    public string ActionRequired { get; set; } = string.Empty;

    /// <summary>
    /// Giá chốt của phiên (hoặc giá mua chào bán của Á quân)
    /// </summary>
    public decimal FinalPrice { get; set; }

    /// <summary>
    /// Số tiền cọc đang bị phong tỏa (được khấu trừ khi thanh toán)
    /// </summary>
    public decimal DepositLocked { get; set; }

    /// <summary>
    /// Số tiền thực tế còn phải thanh toán (FinalPrice - DepositLocked)
    /// </summary>
    public decimal RemainingPaymentRequired { get; set; }

    /// <summary>
    /// Số dư khả dụng hiện tại trong ví của người dùng
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// Số tiền còn thiếu cần nạp thêm nếu số dư khả dụng không đủ (Max(0, RemainingPaymentRequired - AvailableBalance))
    /// </summary>
    public decimal DeficitAmount { get; set; }

    /// <summary>
    /// Người dùng có thể thanh toán trực tiếp ngay bây giờ hay không (DeficitAmount == 0)
    /// </summary>
    public bool CanDirectPay { get; set; }

    /// <summary>
    /// Hạn chót thực hiện hành động
    /// </summary>
    public DateTime? Deadline { get; set; }

    /// <summary>
    /// Số giây còn lại đến hạn chót (nếu chưa hết hạn)
    /// </summary>
    public double? RemainingSeconds { get; set; }

    /// <summary>
    /// Thông tin giá chào bán của Á quân (khi Seller cần duyệt)
    /// </summary>
    public decimal? RunnerUpFinalPrice { get; set; }

    /// <summary>
    /// ID người tham gia của Á quân
    /// </summary>
    public Guid? RunnerUpParticipantId { get; set; }
}
