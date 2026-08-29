using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.CreateCarrier;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetSimCarriers;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý tài nguyên Nhà mạng viễn thông (Viettel, VinaPhone, MobiFone...).
/// </summary>
[ApiController]
[Route("api/carriers")]
[Produces("application/json")]
public class CarriersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CarriersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách tất cả nhà mạng kèm đầu số (Dành cho User/Guest/Consultant).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SimCarrierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCarriers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSimCarriersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Thêm mới nhà mạng viễn thông vào hệ thống (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCarrier(
        [FromBody] CreateCarrierCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
