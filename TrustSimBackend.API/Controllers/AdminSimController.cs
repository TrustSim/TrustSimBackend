using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetAdminSims;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý toàn bộ kho SIM hệ thống dành cho Quản trị viên (Admin).
/// </summary>
[ApiController]
[Route("api/admin/sims")]
[Authorize(Roles = "ADMIN")]
[Produces("application/json")]
public class AdminSimController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminSimController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ kho SIM hệ thống (Có phân trang và lọc đa tiêu chí theo Nhà mạng, Danh mục, Trạng thái).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAdminSims(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] SimStatus? status = null,
        [FromQuery] Guid? carrierId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminSimsQuery(pageIndex, pageSize, status, carrierId, categoryId, search);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
