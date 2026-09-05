using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.Commands;
using TrustSimBackend.Application.Features.Dispute.DTOs;
using TrustSimBackend.Application.Features.Dispute.Queries;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class DisputesController : BaseApiController
{
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
        var isAdminOrConsultant = IsAdmin || IsConsultant;
        var query = new GetDisputesQuery(isAdminOrConsultant ? null : CurrentUserId, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
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
        var result = await Mediator.Send(new GetDisputeByIdQuery(id));
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
        var command = new CreateDisputeCommand(CurrentUserId, request.TradeTransactionId, request.Reason);
        var result = await Mediator.Send(command);
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
        var command = new ResolveDisputeCommand(id, CurrentUserId, request.ResolutionStatus, request.ResolutionNotes);
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
