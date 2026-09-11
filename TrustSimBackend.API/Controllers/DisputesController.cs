using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Dispute.DTOs.Requests;
using TrustSimBackend.Application.Features.Dispute.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class DisputesController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách khiếu nại tranh chấp (có phân trang và lọc theo trạng thái)
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<DisputeTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisputes(
        [FromQuery] DisputeStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetDisputesQuery(IsAdmin ? null : CurrentUserId, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<DisputeTicketDto>>.SuccessResult(result, "Lấy danh sách khiếu nại thành công."));
    }

    /// <summary>
    /// Lấy chi tiết một khiếu nại tranh chấp theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DisputeTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetDisputeByIdQuery(id));
        return Ok(ApiResponse<DisputeTicketDto>.SuccessResult(result, "Lấy thông tin khiếu nại thành công."));
    }

    /// <summary>
    /// Tạo yêu cầu khiếu nại / mở tranh chấp giao dịch
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DisputeTicketDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDisputeCommand command)
    {
        var result = await Mediator.Send(command with { ComplainantId = CurrentUserId });
        return Ok(ApiResponse<DisputeTicketDto>.SuccessResult(result, "Mở khiếu nại tranh chấp thành công."));
    }

    /// <summary>
    /// [Admin] Phán quyết và giải quyết khiếu nại tranh chấp
    /// </summary>
    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromRoute] Guid id, [FromBody] ResolveDisputeCommand command)
    {
        var result = await Mediator.Send(command with { TicketId = id, AdminId = CurrentUserId });
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xử lý phán quyết khiếu nại thành công."));
    }

    /// <summary>
    /// [Admin] Xuất báo cáo khiếu nại và tranh chấp ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminExportDisputes([FromQuery] DisputeStatus? status)
    {
        var bytes = await Mediator.Send(new AdminExportDisputesQuery(status));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Disputes_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }
}

