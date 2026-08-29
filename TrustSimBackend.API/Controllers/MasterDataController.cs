using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.CreateCarrier;
using TrustSimBackend.Application.Features.Sim.Commands.CreateCategory;
using TrustSimBackend.Application.Features.Sim.Commands.SeedMasterData;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetFormatRhythms;
using TrustSimBackend.Application.Features.Sim.Queries.GetSimCarriers;
using TrustSimBackend.Application.Features.Sim.Queries.GetSimCategories;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý dữ liệu danh mục nền (Master Data): Nhà mạng, Đầu số, Danh mục phân loại số đẹp, Kiểu nhịp định dạng.
/// </summary>
[ApiController]
[Route("api/master-data")]
[Produces("application/json")]
public class MasterDataController : ControllerBase
{
    private readonly IMediator _mediator;

    public MasterDataController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách tất cả các nhà mạng viễn thông kèm danh sách đầu số (Viettel, VinaPhone, MobiFone...).
    /// Dùng cho User khi đăng ký SIM, lọc trên sàn, hoặc Consultant khi tra cứu.
    /// </summary>
    [HttpGet("carriers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SimCarrierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCarriers(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSimCarriersQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách tất cả danh mục phân loại SIM (Tứ quý, Ngũ quý, Thần tài, Lộc phát...) kèm hệ số định giá.
    /// </summary>
    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<SimCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSimCategoriesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách các kiểu nhịp định dạng số SIM (Sảnh tiến, Lặp đôi, Tam hoa kép, Gánh đảo...).
    /// </summary>
    [HttpGet("format-rhythms")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<FormatRhythmDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormatRhythms(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFormatRhythmsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Nạp dữ liệu nền mẫu chuẩn Việt Nam (6 nhà mạng, 30+ đầu số, 12 danh mục VIP, 6 kiểu nhịp định dạng) vào database.
    /// </summary>
    [HttpPost("seed")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedMasterData(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SeedSimMasterDataCommand(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tạo thêm nhà mạng mới kèm đầu số (Dành cho Admin).
    /// </summary>
    [HttpPost("carriers")]
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

    /// <summary>
    /// Tạo thêm danh mục phân loại SIM mới (Dành cho Admin).
    /// </summary>
    [HttpPost("categories")]
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
