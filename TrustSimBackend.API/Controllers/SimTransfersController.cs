using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller quản lý tiến trình bàn giao SIM, tiếp nhận biên bản sang tên nhà mạng và xác nhận giải ngân Escrow.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SimTransfersController : BaseApiController
{
    /// <summary>
    /// Lấy thông tin chi tiết tiến trình bàn giao SIM và trạng thái giải ngân Escrow của giao dịch.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    [HttpGet("{tradeId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails([FromRoute] Guid tradeId)
    {
        var result = await Mediator.Send(new GetSimTransferDetailsQuery(tradeId, CurrentUserId));
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, "Lấy thông tin tiến trình bàn giao SIM thành công."));
    }

    /// <summary>
    /// [Bên Bán] Nộp ảnh bằng chứng/biên bản chuyển nhượng SIM từ điểm giao dịch nhà mạng.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    /// <param name="request">Chứa đường dẫn ảnh biên bản và mô tả.</param>
    [HttpPost("{tradeId:guid}/proof")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitProof([FromRoute] Guid tradeId, [FromBody] SubmitProofRequest request)
    {
        var command = new SubmitTransferProofCommand(
            TradeId: tradeId,
            UserId: CurrentUserId,
            ImageUrl: request.ImageUrl,
            Description: request.Description
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Nộp biên bản sang tên thành công."));
    }

    /// <summary>
    /// [Bên Mua] Xác nhận đã nhận SIM có sóng, hoàn tất chuyển giao quyền sở hữu SIM và giải ngân tiền Escrow cho Người Bán.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    [HttpPost("{tradeId:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmTransfer([FromRoute] Guid tradeId)
    {
        var command = new ConfirmSimTransferCommand(
            TradeId: tradeId,
            UserId: CurrentUserId
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Xác nhận nhận SIM và giải ngân thành công."));
    }
}
