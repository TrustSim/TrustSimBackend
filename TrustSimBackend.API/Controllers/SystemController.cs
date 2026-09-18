using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Các tiện ích quản trị hệ thống và khởi tạo dữ liệu mẫu (System Utilities).
/// </summary>
public class SystemController : BaseApiController
{
    /// <summary>
    /// Nạp dữ liệu nền mẫu chuẩn Việt Nam (6 nhà mạng, 30+ đầu số, 12 danh mục VIP, 6 kiểu nhịp định dạng) vào database.
    /// </summary>
    [HttpPost("seed-master-data")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedMasterData(CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SeedSimMasterDataCommand(), cancellationToken);
        return Ok(ApiResponse<string>.SuccessResult(result, result));
    }
}

