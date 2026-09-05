using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.Commands.ChangePassword;
using TrustSimBackend.Application.Features.Identity.Commands.ForgotPassword;
using TrustSimBackend.Application.Features.Identity.Commands.LoginUser;
using TrustSimBackend.Application.Features.Identity.Commands.Logout;
using TrustSimBackend.Application.Features.Identity.Commands.RefreshToken;
using TrustSimBackend.Application.Features.Identity.Commands.RegisterUser;
using TrustSimBackend.Application.Features.Identity.Commands.ResetPassword;
using TrustSimBackend.Application.Features.Identity.Commands.UpdateAvatar;
using TrustSimBackend.Application.Features.Identity.Commands.UpdateProfile;
using TrustSimBackend.Application.Features.Identity.DTOs;
using TrustSimBackend.Application.Features.Identity.Queries.GetUserProfile;

namespace TrustSimBackend.API.Controllers;

public class AuthController : BaseApiController
{
    /// <summary>
    /// Đăng ký tài khoản người dùng mới (Tự động thiết lập HttpOnly Cookie accessToken và refreshToken)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập hệ thống bằng Username/Email và Password (Tự động thiết lập HttpOnly Cookie accessToken và refreshToken)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Làm mới Access Token bằng Refresh Token (Tự động lấy từ HttpOnly Cookie hoặc qua Request Body)
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequestDto? request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var command = new RefreshTokenCommand(request?.RefreshToken, ipAddress);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đăng xuất khỏi hệ thống (Thu hồi Refresh Token và xóa HttpOnly Cookies)
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequestDto? request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var command = new LogoutCommand(request?.RefreshToken, ipAddress);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin hồ sơ của người dùng đang đăng nhập (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpGet("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var result = await Mediator.Send(new GetUserProfileQuery(CurrentUserId));
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật hồ sơ cá nhân: Họ tên, Ngày sinh, Địa chỉ, CCCD, Số điện thoại (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var command = new UpdateProfileCommand(
            CurrentUserId,
            request.FullName,
            request.DateOfBirth,
            request.Address,
            request.IdentityNumber,
            request.PhoneNumber
        );

        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Đổi mật khẩu tài khoản (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var command = new ChangePasswordCommand(
            CurrentUserId,
            request.CurrentPassword,
            request.NewPassword,
            request.ConfirmNewPassword
        );

        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật ảnh đại diện Avatar (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpPut("avatar")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarRequest request)
    {
        var command = new UpdateAvatarCommand(CurrentUserId, request.AvatarUrl);
        var result = await Mediator.Send(command);
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
        var result = await Mediator.Send(command);
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
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
