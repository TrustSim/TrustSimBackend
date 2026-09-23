using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

public class AuthController : BaseApiController
{
    
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<RegisterInitiatedDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterUserCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<RegisterInitiatedDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Bước 2 Đăng ký: Xác thực mã Email OTP. Khi xác thực thành công, hệ thống chính thức tạo tài khoản vào DB, thiết lập HttpOnly Cookies và tự động đăng nhập.
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<AuthResponseDto>.SuccessResult(result, "Xác thực email và tạo tài khoản thành công."));
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
        return Ok(ApiResponse<AuthResponseDto>.SuccessResult(result, "Đăng nhập thành công."));
    }

    /// <summary>
    /// Làm mới Access Token (Tự động trích xuất Refresh Token từ HttpOnly Cookie / Context hiện tại, không yêu cầu truyền Body)
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var cmd = new RefreshTokenCommand(IpAddress: ipAddress);
        var result = await Mediator.Send(cmd);
        return Ok(ApiResponse<AuthResponseDto>.SuccessResult(result, "Làm mới Access Token thành công."));
    }

    /// <summary>
    /// Đăng xuất khỏi phiên làm việc hiện tại (Thu hồi Refresh Token, đưa Access Token vào Blacklist và xóa HttpOnly Cookies).
    /// Toàn bộ thông tin Access Token, Refresh Token và IP được tự động trích xuất từ Headers, Cookies và HttpContext.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var accessToken = Request.Headers.Authorization.FirstOrDefault();
        if (!string.IsNullOrEmpty(accessToken) && accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = accessToken["Bearer ".Length..].Trim();
        }
        else if (Request.Cookies.TryGetValue("accessToken", out var cookieToken))
        {
            accessToken = cookieToken;
        }

        string? refreshToken = null;
        if (Request.Cookies.TryGetValue("refreshToken", out var cookieRefreshToken))
        {
            refreshToken = cookieRefreshToken;
        }

        var cmd = new LogoutCommand(refreshToken, accessToken, ipAddress);
        var result = await Mediator.Send(cmd);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đăng xuất thành công."));
    }

    /// <summary>
    /// Đăng xuất khỏi tất cả các thiết bị và thu hồi toàn bộ Access / Refresh Token của tài khoản
    /// </summary>
    [HttpPost("revoke-all")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await Mediator.Send(new RevokeAllSessionsCommand(CurrentUserId, ipAddress));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đã đăng xuất và hủy bỏ toàn bộ các phiên hoạt động thành công."));
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
        return Ok(ApiResponse<UserProfileDto>.SuccessResult(result, "Lấy thông tin hồ sơ thành công."));
    }

    /// <summary>
    /// Cập nhật hồ sơ cá nhân: Họ tên, Ngày sinh, Địa chỉ, CCCD, Số điện thoại (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpPut("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<UserProfileDto>.SuccessResult(result, "Cập nhật hồ sơ cá nhân thành công."));
    }

    /// <summary>
    /// Đổi mật khẩu tài khoản dành cho người dùng đang đăng nhập (Yêu cầu nhập mật khẩu hiện tại)
    /// </summary>
    [HttpPut("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đổi mật khẩu thành công."));
    }

    /// <summary>
    /// Cập nhật ảnh đại diện Avatar (Yêu cầu xác thực qua Middleware)
    /// </summary>
    [HttpPut("avatar")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateAvatar([FromBody] UpdateAvatarCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<string>.SuccessResult(result, "Cập nhật ảnh đại diện thành công."));
    }

    /// <summary>
    /// Bước 1 Quên mật khẩu: Gửi mã OTP xác thực tới Email của người dùng
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<string>.SuccessResult(result, "Mã xác thực đặt lại mật khẩu đã được gửi thành công."));
    }

    /// <summary>
    /// Bước 2 Quên mật khẩu: Xác thực mã OTP và đặt lại mật khẩu mới cho tài khoản
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đặt lại mật khẩu mới thành công."));
    }
}
