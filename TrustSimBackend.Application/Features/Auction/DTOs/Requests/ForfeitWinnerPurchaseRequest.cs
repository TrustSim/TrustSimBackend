namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// DTO chứa thông tin lý do khi Người thắng chủ động từ bỏ quyền mua SIM.
/// </summary>
public record ForfeitWinnerPurchaseRequest(
    string? Reason = null
);
