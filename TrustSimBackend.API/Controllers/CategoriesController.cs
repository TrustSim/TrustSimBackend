using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.CreateCategory;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetSimCategories;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý tài nguyên Danh mục phân loại SIM (Tứ quý, Thần tài, Lộc phát...).
/// </summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CategoriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách toàn bộ danh mục SIM kèm hệ số định giá (Dành cho User/Guest/Consultant).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SimCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSimCategoriesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Thêm danh mục phân loại SIM mới (Chỉ dành cho ADMIN).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateCategory(
        [FromBody] CreateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
