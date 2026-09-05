using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.Commands.Prefixes;
using TrustSimBackend.Application.Features.Sim.DTOs;
using TrustSimBackend.Application.Features.Sim.Queries.Prefixes;

namespace TrustSimBackend.API.Controllers;

public class PhonePrefixesController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách các đầu số (có thể lọc theo nhà mạng)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PhonePrefixDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? carrierId)
    {
        var result = await Mediator.Send(new GetPrefixesQuery(carrierId));
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Thêm mới đầu số nhà mạng (098, 090, 088...)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PhonePrefixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePrefixRequest request)
    {
        var command = new CreatePrefixCommand(request.PrefixValue, request.SimCarrierId);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Xóa đầu số nhà mạng
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeletePrefixCommand(id));
        return Ok(result);
    }
}
