using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.Commands;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

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
    /// [Sandbox Demo - Cột Trái] Pha 1: Gửi yêu cầu rút tiền &amp; Khóa số dư (Nợ 2010 / Có 2020) -- Trạng thái PENDING
    /// </summary>
    [HttpPost("phase1-request")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestWithdrawPhase1([FromBody] CreateWithdrawRequest request)
    {
        var idempotencyKey = request.IdempotencyKey
            ?? (Request.Headers.TryGetValue("Idempotency-Key", out var headerKey) ? headerKey.ToString() : null)
            ?? (Request.Headers.TryGetValue("X-Idempotency-Key", out var xHeaderKey) ? xHeaderKey.ToString() : null);

        var command = new CreateWithdrawPhase1Command(
            CurrentUserId,
            request.Amount,
            request.DestinationBankCode,
            request.DestinationBankName,
            request.DestinationAccountNumber,
            request.DestinationAccountHolderName,
            request.Note,
            idempotencyKey);

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<WalletTransactionDto>.SuccessResult(result, "Khởi tạo yêu cầu rút tiền Pha 1 và khóa số dư thành công (PENDING)."));
    }

    /// <summary>
    /// [Sandbox Demo - Cột Phải Nút Xanh] Pha 2A: Giả lập ngân hàng chi trả THÀNH CÔNG (Nợ 2020 / Có 1010) -- Trạng thái COMPLETED
    /// </summary>
    [HttpPost("phase2a-success")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulatePhase2aSuccess([FromBody] ProcessWithdrawSuccessRequest request)
    {
        var command = new ProcessWithdrawSuccessCommand(request.TransactionCode, request.ExternalBankRef);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<WalletTransactionDto>.SuccessResult(result, $"Pha 2A Hoàn tất: Chi trả thành công mã GD '{request.TransactionCode}' (COMPLETED)."));
    }

    /// <summary>
    /// [Sandbox Demo - Cột Phải Nút Đỏ] Pha 2B: Giả lập ngân hàng chi trả THẤT BẠI &amp; Tự động hoàn tiền (Nợ 2020 / Có 2010) -- Trạng thái CANCELLED
    /// </summary>
    [HttpPost("phase2b-failed")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SimulatePhase2bFailed([FromBody] ProcessWithdrawFailedRequest request)
    {
        var command = new ProcessWithdrawFailedCommand(request.TransactionCode, request.FailureReason);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<WalletTransactionDto>.SuccessResult(result, $"Pha 2B Hoàn tất: Giao dịch thất bại và đã tự động hoàn tiền về ví (CANCELLED)."));
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
