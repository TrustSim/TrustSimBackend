using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.Carriers;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.Carriers;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SimCarriersController : ControllerBase
{
    private readonly IMediator _mediator;

    public SimCarriersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách tất cả các nhà mạng viễn thông
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SimCarrierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await _mediator.Send(new GetCarriersQuery());
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin chi tiết nhà mạng kèm các đầu số
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new GetCarrierByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Thêm mới nhà mạng viễn thông
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCarrierRequest request)
    {
        var command = new CreateCarrierCommand(request.CarrierName, request.CarrierCode, request.LogoImg);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin nhà mạng
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCarrierRequest request)
    {
        var command = new UpdateCarrierCommand(id, request.CarrierName, request.LogoImg);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) nhà mạng
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await _mediator.Send(new DeleteCarrierCommand(id));
        return Ok(result);
    }
}
