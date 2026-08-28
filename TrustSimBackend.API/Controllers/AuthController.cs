using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.Commands.ChangePassword;
using TrustSimBackend.Application.Features.Identity.Commands.ForgotPassword;
using TrustSimBackend.Application.Features.Identity.Commands.LoginUser;
using TrustSimBackend.Application.Features.Identity.Commands.RegisterUser;
using TrustSimBackend.Application.Features.Identity.Commands.ResetPassword;
using TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;
using TrustSimBackend.Application.Features.Identity.Commands.UpdateProfile;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Application.Features.Identity.Queries.GetUserProfile;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Đăng ký tài khoản người dùng mới (Bắt buộc CCCD 12 số)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập hệ thống bằng Username/Email và Password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin hồ sơ của người dùng đang đăng nhập (Yêu cầu Token)
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetUserProfileQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật hồ sơ cá nhân: Họ tên, Ngày sinh, Địa chỉ, CCCD, Số điện thoại (Yêu cầu Token)
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new UpdateProfileCommand(
            userId,
            request.FullName,
            request.DateOfBirth,
            request.Address,
            request.IdentityNumber,
            request.PhoneNumber
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đổi mật khẩu tài khoản (Yêu cầu Token)
    /// </summary>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new ChangePasswordCommand(
            userId,
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmNewPassword
        );

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật ảnh đại diện Avatar (Yêu cầu Token)
    /// </summary>
    [HttpPut("avatar")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new UpdateAvatarCommand(userId, request.AvatarUrl);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Yêu cầu gửi mã xác thực khi quên mật khẩu
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đặt lại mật khẩu mới bằng mã xác thực đã nhận
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
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

// ─── DTO Request Models cho Controller ──────────────────────────────────────────
public record UpdateProfileRequest(
    string FullName,
    DateTime? DateOfBirth,
    string Address,
    string IdentityNumber,
    string PhoneNumber
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmNewPassword
);

public record UpdateAvatarRequest(
    string AvatarUrl
);
