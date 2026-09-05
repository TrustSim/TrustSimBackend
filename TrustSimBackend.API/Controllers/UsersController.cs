using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class UsersController : BaseApiController
{
    /// <summary>
    /// Người dùng gửi hồ sơ xác minh danh tính KYC (CCCD và ảnh chân dung)
    /// </summary>
    [HttpPost("kyc")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitKyc([FromBody] SubmitKycCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<IdentityVerificationDto>.SuccessResult(result, "Gửi hồ sơ xác minh KYC thành công. Vui lòng chờ phê duyệt."));
    }

    /// <summary>
    /// Lấy trạng thái hồ sơ KYC gần nhất của người dùng hiện tại
    /// </summary>
    [HttpGet("kyc/status")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyKycStatus()
    {
        var result = await Mediator.Send(new GetMyKycStatusQuery(CurrentUserId));
        return Ok(ApiResponse<IdentityVerificationDto>.SuccessResult(result, "Lấy thông tin trạng thái KYC thành công."));
    }

    /// <summary>
    /// [Admin] Lấy danh sách các hồ sơ KYC chờ duyệt (có phân trang và lọc theo trạng thái)
    /// </summary>
    [HttpGet("admin/kyc")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IdentityVerificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingKyc(
        [FromQuery] VerificationStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetPendingKycQuery(status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<IdentityVerificationDto>>.SuccessResult(result, "Lấy danh sách hồ sơ KYC thành công."));
    }

    /// <summary>
    /// [Admin] Phê duyệt hoặc từ chối hồ sơ xác minh KYC
    /// </summary>
    [HttpPost("admin/kyc/{verificationId:guid}/review")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewKyc([FromRoute] Guid verificationId, [FromBody] ReviewKycCommand command)
    {
        var result = await Mediator.Send(command with { VerificationId = verificationId });
        return Ok(ApiResponse<bool>.SuccessResult(result, "Duyệt hồ sơ xác minh KYC thành công."));
    }
}
