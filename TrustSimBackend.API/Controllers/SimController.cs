using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.RegisterSim;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetMySims;
using TrustSimBackend.Application.Features.Sim.Queries.GetSimDetail;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý số SIM dành cho Người dùng / Người bán (Seller).
/// </summary>
[ApiController]
[Route("api/sims")]
[Produces("application/json")]
public class SimController : ControllerBase
{
    private readonly IMediator _mediator;

    public SimController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Đăng ký số SIM mới vào hệ thống (Dành cho Người bán).
    /// </summary>
    /// <remarks>
    /// Người dùng phải đăng nhập. Hệ thống sẽ tự động gán quyền sở hữu ban đầu và tạo yêu cầu xác minh (Pending).
    /// </remarks>
    [HttpPost("register")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RegisterSim(
        [FromBody] RegisterSimCommand command,
        CancellationToken cancellationToken)
    {
        // Trích xuất UserId từ JWT Claim
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userAccountId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));
        }

        command.UserAccountId = userAccountId;
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách kho SIM thuộc sở hữu của Người dùng hiện tại (Có phân trang).
    /// </summary>
    [HttpGet("my-sims")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMySims(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] SimStatus? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userAccountId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));
        }

        var query = new GetMySimsQuery(pageIndex, pageSize, status, search)
        {
            UserAccountId = userAccountId
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem thông tin chi tiết một số SIM theo ID (Nhà mạng, Đầu số, Danh mục, Nhịp định dạng, Người sở hữu).
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SimDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSimDetail(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSimDetailQuery(id), cancellationToken);
        return Ok(result);
    }
}
