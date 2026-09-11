using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.Commands;

namespace TrustSimBackend.API.Controllers;

public class OtpController : BaseApiController
{
    private readonly ISpeedSmsService _speedSmsService;

    public OtpController(ISpeedSmsService speedSmsService)
    {
        _speedSmsService = speedSmsService;
    }

    /// <summary>
    /// Gửi mã xác thực OTP qua SMS tới số điện thoại (Hỗ trợ SMS Gateway/Brandname SpeedSMS).
    /// </summary>
    [HttpPost("send-phone-otp")]
    [ProducesResponseType(typeof(ApiResponse<SendOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> SendPhoneOtp([FromBody] SendPhoneOtpCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(ApiResponse<SendOtpResponseDto>.FailureResult(result.Message));
        }
        return Ok(ApiResponse<SendOtpResponseDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Xác thực mã OTP 6 chữ số do người dùng nhập vào.
    /// </summary>
    [HttpPost("verify-phone-otp")]
    [ProducesResponseType(typeof(ApiResponse<VerifyOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyPhoneOtp([FromBody] VerifyPhoneOtpCommand command, CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(command, cancellationToken);
        if (!result.IsValid)
        {
            return BadRequest(ApiResponse<VerifyOtpResponseDto>.FailureResult(result.Message));
        }
        return Ok(ApiResponse<VerifyOtpResponseDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Tra cứu thông tin tài khoản SpeedSMS và số dư hiện tại (Dành cho Quản trị viên).
    /// </summary>
    [HttpGet("speedsms-account-info")]
    [ProducesResponseType(typeof(ApiResponse<SpeedSmsUserInfoResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpeedSmsAccountInfo(CancellationToken cancellationToken)
    {
        var info = await _speedSmsService.GetUserInfoAsync(cancellationToken);
        if (info == null || !info.IsSuccess)
        {
            return BadRequest(ApiResponse<SpeedSmsUserInfoResponse?>.FailureResult(info?.Message ?? "Không thể lấy thông tin SpeedSMS."));
        }
        return Ok(ApiResponse<SpeedSmsUserInfoResponse>.SuccessResult(info, "Lấy thông tin tài khoản SpeedSMS thành công."));
    }
}
