using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

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
        return Ok(ApiResponse<IReadOnlyList<PhonePrefixDto>>.SuccessResult(result, "Lấy danh sách đầu số thành công."));
    }

    /// <summary>
    /// [Admin] Thêm mới đầu số nhà mạng (098, 090, 088...)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PhonePrefixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePrefixCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<PhonePrefixDto>.SuccessResult(result, "Thêm mới đầu số thành công."));
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
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa đầu số thành công."));
    }
}
