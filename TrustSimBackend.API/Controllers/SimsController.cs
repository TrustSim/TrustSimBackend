using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.Sims;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.Sims;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SimsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Tìm kiếm và lọc danh sách SIM số đẹp (phân trang, lọc theo nhà mạng, thể loại, đầu số, từ khóa)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimNumberDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSims(
        [FromQuery] string? keyword,
        [FromQuery] Guid? carrierId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? prefixId,
        [FromQuery] SimStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetSimsQuery(keyword, carrierId, categoryId, prefixId, status, pageIndex, pageSize);
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết một số SIM
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new GetSimByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [Admin/Consultant] Thêm mới số SIM vào kho
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSimRequest request)
    {
        var command = new CreateSimCommand(request.PhoneNumber, request.PhonePrefixId, request.FormatRhythmId, request.CategoryIds);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin/Consultant] Cập nhật thông tin và trạng thái số SIM
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSimRequest request)
    {
        var command = new UpdateSimCommand(id, request.SimStatus, request.PhonePrefixId, request.FormatRhythmId, request.CategoryIds);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) số SIM khỏi hệ thống
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new DeleteSimCommand(id));
        return Ok(result);
    }
}
