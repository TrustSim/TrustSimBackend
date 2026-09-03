using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.Commands;
using TrustSimBackend.Application.Features.Dispute.DTOs;
using TrustSimBackend.Application.Features.Dispute.Queries;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DisputesController : ControllerBase
{
    private readonly IMediator _mediator;

    public DisputesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách khiếu nại (User xem của mình, Admin xem tất cả)
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DisputeTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisputes(
        [FromQuery] DisputeStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var isAdmin = User.IsInRole("ADMIN") || User.IsInRole("CONSULTANT");
        var query = new GetDisputesQuery(isAdmin ? null : userId, status, pageIndex, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một khiếu nại
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DisputeTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new GetDisputeByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu khiếu nại / mở tranh chấp giao dịch
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DisputeTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDisputeRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new CreateDisputeCommand(userId, request.TradeTransactionId, request.Reason);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin/Consultant] Đưa ra phán quyết và giải quyết khiếu nại
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromRoute] Guid id, [FromBody] ResolveDisputeRequest request)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new ResolveDisputeCommand(id, adminId, request.ResolutionStatus, request.ResolutionNotes);
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
