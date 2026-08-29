using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.GetFormatRhythms;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý tài nguyên Kiểu nhịp định dạng số SIM (Sảnh tiến, Lặp đôi, Tam hoa kép...).
/// </summary>
[ApiController]
[Route("api/format-rhythms")]
[Produces("application/json")]
public class FormatRhythmsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FormatRhythmsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách các kiểu nhịp định dạng số SIM (Dành cho User/Guest/Consultant).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<FormatRhythmDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormatRhythms(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetFormatRhythmsQuery(), cancellationToken);
        return Ok(result);
    }
}
