using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.Commands.Kyc;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Application.Features.Identity.Queries.Kyc;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Người dùng gửi hồ sơ xác minh danh tính KYC (CCCD &amp; ảnh chân dung)
    /// </summary>
    [HttpPost("kyc")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IdentityVerificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitKyc([FromBody] SubmitKycRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new SubmitKycCommand(userId, request.IdentityNumber, request.AvatarImg);
        var result = await _mediator.Send(command);
        return Ok(result);
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetMyKycStatusQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Lấy danh sách các hồ sơ KYC chờ duyệt (có phân trang &amp; lọc theo trạng thái)
    /// </summary>
    [HttpGet("admin/kyc")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<IdentityVerificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingKyc(
        [FromQuery] VerificationStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetPendingKycQuery(status, pageIndex, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Phê duyệt hoặc từ chối hồ sơ xác minh KYC
    /// </summary>
    [HttpPost("admin/kyc/{verificationId:guid}/review")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewKyc([FromRoute] Guid verificationId, [FromBody] ReviewKycRequest request)
    {
        var command = new ReviewKycCommand(verificationId, request.Status, request.RejectionReason);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out userId))
        {
            return true;
        }
        userId = Guid.Empty;
        return false;
    }
}
