using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs;
using TrustSimBackend.Application.Features.Trade.Queries;
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
        var result = await Mediator.Send(new GetMyTradeTransactionsQuery(CurrentUserId, status, pageIndex, pageSize));
        return Ok(result);
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
        return Ok(result);
    }

    /// <summary>
    /// Khởi tạo giao dịch chuyển nhượng SIM (tạo tài khoản ký quỹ Escrow bảo đảm)
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TradeTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateTradeRequest request)
    {
        var command = new CreateTradeTransactionCommand(CurrentUserId, request.SellerId, request.SimNumberId, request.AuctionId, request.EscrowAmount);
        var result = await Mediator.Send(command);
        return Ok(result);
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
        return Ok(result);
    }
}
