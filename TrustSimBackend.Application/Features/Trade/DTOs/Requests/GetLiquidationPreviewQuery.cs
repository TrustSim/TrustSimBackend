using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Truy vấn nội dung xem trước Biên bản nghiệm thu và thanh lý giao dịch điện tử.
/// </summary>
/// <param name="TradeId">ID giao dịch chuyển nhượng.</param>
/// <param name="CurrentUserId">ID người dùng đang xem.</param>
public record GetLiquidationPreviewQuery(
    Guid TradeId,
    Guid CurrentUserId,
    bool IsAdmin = false
) : IRequest<LiquidationPreviewDto>;
