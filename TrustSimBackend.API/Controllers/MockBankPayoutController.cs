using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Cổng giả lập chi hộ Ngân hàng (Mock Bank Payout Sandbox Gateway).
/// Phục vụ kịch bản thử nghiệm và Demo toàn trình rút tiền tự động mà không cần liên kết ngân hàng thật.
/// </summary>
public class MockBankPayoutController : BaseApiController
{
    private readonly IMockBankPayoutGatewayService _mockBankPayoutService;

    public MockBankPayoutController(IMockBankPayoutGatewayService mockBankPayoutService)
    {
        _mockBankPayoutService = mockBankPayoutService;
    }

    /// <summary>
    /// [Sandbox Demo] Giả lập Webhook Callback từ ngân hàng xác nhận kết quả chi trả tiền rút (Pha 2A thành công / Pha 2B thất bại)
    /// </summary>
    /// <param name="request">Thông tin kết quả chi trả ngân hàng</param>
    /// <returns>Kết quả xử lý</returns>
    [HttpPost("simulate-callback")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulatePayoutCallback([FromBody] ProcessWithdrawCallbackRequest request)
    {
        var result = await _mockBankPayoutService.SimulateBankPayoutCallbackAsync(
            request.TransactionCode,
            request.IsSuccess,
            request.FailureReason);

        var msg = request.IsSuccess
            ? $"Giả lập Payout THÀNH CÔNG cho mã GD '{request.TransactionCode}' (Pha 2A: Nợ 2020 / Có 1010)."
            : $"Giả lập Payout THẤT BẠI cho mã GD '{request.TransactionCode}'. Hệ thống đã tự động hoàn tiền (Pha 2B: Nợ 2020 / Có 2010).";

        return Ok(ApiResponse<bool>.SuccessResult(result, msg));
    }
}
