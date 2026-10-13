using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Truy vấn nội dung xem trước hợp đồng chuyển nhượng SIM dạng HTML cùng trạng thái ký của 2 bên.
/// </summary>
/// <param name="TradeId">ID giao dịch chuyển nhượng.</param>
/// <param name="CurrentUserId">ID người dùng đang xem.</param>
public record GetContractPreviewQuery(
    Guid TradeId,
    Guid CurrentUserId
) : IRequest<ContractPreviewDto>;
