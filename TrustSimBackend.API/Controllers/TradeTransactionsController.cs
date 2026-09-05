using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class TradeTransactionsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách các giao dịch mua/bán SIM của người dùng hiện tại
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TradeTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTrades(
        [FromQuery] TradeTransactionStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetMyTradeTransactionsQuery(CurrentUserId, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<TradeTransactionDto>>.SuccessResult(result, "Lấy danh sách giao dịch thành công."));
    }

    /// <summary>
    /// Lấy chi tiết một giao dịch mua bán SIM
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TradeTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetTradeTransactionByIdQuery(id, CurrentUserId));
        return Ok(ApiResponse<TradeTransactionDto>.SuccessResult(result, "Lấy thông tin giao dịch thành công."));
    }

    /// <summary>
    /// Khởi tạo giao dịch chuyển nhượng SIM (tạo tài khoản ký quỹ Escrow bảo đảm)
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TradeTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTradeTransactionCommand command)
    {
        var result = await Mediator.Send(command with { BuyerId = CurrentUserId });
        return Ok(ApiResponse<TradeTransactionDto>.SuccessResult(result, "Tạo giao dịch chuyển nhượng SIM thành công."));
    }

    /// <summary>
    /// [Admin/Consultant] Cập nhật trạng thái tiến trình giao dịch
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus([FromRoute] Guid id, [FromQuery] TradeTransactionStatus newStatus)
    {
        var command = new UpdateTradeStatusCommand(id, newStatus);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Cập nhật trạng thái giao dịch thành công."));
    }
}
