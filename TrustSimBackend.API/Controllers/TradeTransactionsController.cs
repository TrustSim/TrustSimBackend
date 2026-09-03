using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.Commands;
using TrustSimBackend.Application.Features.Trade.DTOs;
using TrustSimBackend.Application.Features.Trade.Queries;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TradeTransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TradeTransactionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetMyTradeTransactionsQuery(userId, status, pageIndex, pageSize));
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetTradeTransactionByIdQuery(id, userId));
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
        if (!TryGetUserId(out var buyerId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new CreateTradeTransactionCommand(buyerId, request.SellerId, request.SimNumberId, request.AuctionId, request.EscrowAmount);
        var result = await _mediator.Send(command);
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
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }
        userId = Guid.Empty;
        return false;
    }
}
