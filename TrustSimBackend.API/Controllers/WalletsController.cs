using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class WalletsController : BaseApiController
{
    /// <summary>
    /// Lấy thông tin ví và số dư của tài khoản đang đăng nhập
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWallet()
    {
        var result = await Mediator.Send(new GetMyWalletQuery(CurrentUserId));
        return Ok(ApiResponse<WalletDto>.SuccessResult(result, "Lấy thông tin ví thành công."));
    }

    /// <summary>
    /// Lấy danh sách lịch sử nạp / rút tiền của ví cá nhân (có phân trang và bộ lọc)
    /// </summary>
    [HttpGet("transactions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] WalletTransactionType? type,
        [FromQuery] TransactionStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetWalletTransactionsQuery(CurrentUserId, type, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<WalletTransactionDto>>.SuccessResult(result, "Lấy lịch sử giao dịch thành công."));
    }

    /// <summary>
    /// Xem thông tin chi tiết một giao dịch ví theo ID
    /// </summary>
    [HttpGet("transactions/{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetWalletTransactionByIdQuery(id, CurrentUserId));
        return Ok(ApiResponse<WalletTransactionDto>.SuccessResult(result, "Lấy thông tin chi tiết giao dịch thành công."));
    }

    /// <summary>
    /// Tạo yêu cầu nạp tiền vào ví -> Trả về thông tin VietQR SePay và cú pháp chuyển khoản
    /// </summary>
    [HttpPost("deposit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DepositResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<DepositResponseDto>.SuccessResult(result, "Tạo yêu cầu nạp tiền thành công. Vui lòng thanh toán theo thông tin hướng dẫn."));
    }

    /// <summary>
    /// Rút tiền từ ví về tài khoản ngân hàng của cá nhân (Hoàn tất tức thì, không cần duyệt)
    /// </summary>
    [HttpPost("withdraw")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWithdraw([FromBody] CreateWithdrawCommand command)
    {
        var result = await Mediator.Send(command with { UserId = CurrentUserId });
        return Ok(ApiResponse<WalletTransactionDto>.SuccessResult(result, "Rút tiền thành công."));
    }

    /// <summary>
    /// Webhook nhận biến động số dư tự động từ cổng thanh toán SePay
    /// </summary>
    [HttpPost("sepay-webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProcessSepayWebhook([FromBody] SepayWebhookDto payload)
    {
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            if (Request.Headers.TryGetValue("X-Api-Key", out var xApiKey))
            {
                authHeader = xApiKey.ToString();
            }
            else if (Request.Headers.TryGetValue("apikey", out var apiKey))
            {
                authHeader = apiKey.ToString();
            }
        }

        var command = new ProcessSepayWebhookCommand(payload, authHeader);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xử lý nạp tiền qua SePay thành công."));
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ADMIN ENDPOINTS (Quản lý và tra cứu giao dịch)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Admin] Lấy danh sách tất cả giao dịch rút tiền trong hệ thống
    /// </summary>
    [HttpGet("admin/withdrawals")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetWithdrawals(
        [FromQuery] TransactionStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new AdminGetWithdrawalsQuery(status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<WalletTransactionDto>>.SuccessResult(result, "Lấy danh sách yêu cầu rút tiền thành công."));
    }

    /// <summary>
    /// [Admin] Phê duyệt giao dịch rút tiền thủ công (nếu có)
    /// </summary>
    [HttpPost("admin/withdrawals/{id:guid}/approve")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminApproveWithdraw([FromRoute] Guid id, [FromQuery] string? note)
    {
        var command = new ApproveWithdrawCommand(id, note, CurrentUserId);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Duyệt yêu cầu rút tiền thành công."));
    }

    /// <summary>
    /// [Admin] Hủy/Từ chối giao dịch rút tiền và hoàn tiền lại vào ví người dùng
    /// </summary>
    [HttpPost("admin/withdrawals/{id:guid}/reject")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminRejectWithdraw([FromRoute] Guid id, [FromBody] RejectWithdrawCommand command)
    {
        var result = await Mediator.Send(command with { TransactionId = id, AdminId = CurrentUserId });
        return Ok(ApiResponse<bool>.SuccessResult(result, "Từ chối yêu cầu rút tiền và hoàn tiền vào ví người dùng thành công."));
    }
}
