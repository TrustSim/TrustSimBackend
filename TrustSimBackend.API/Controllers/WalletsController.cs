using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.Commands.ApproveWithdraw;
using TrustSimBackend.Application.Features.Financial.Commands.CreateDeposit;
using TrustSimBackend.Application.Features.Financial.Commands.CreateWithdraw;
using TrustSimBackend.Application.Features.Financial.Commands.ProcessSepayWebhook;
using TrustSimBackend.Application.Features.Financial.Commands.RejectWithdraw;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Application.Features.Financial.Queries.AdminGetWithdrawals;
using TrustSimBackend.Application.Features.Financial.Queries.GetMyWallet;
using TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactionById;
using TrustSimBackend.Application.Features.Financial.Queries.GetWalletTransactions;
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
        return Ok(result);
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
        [FromQuery] int pageSize = 10)
    {
        var query = new GetWalletTransactionsQuery(CurrentUserId, type, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(result);
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
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu nạp tiền vào ví -> Trả về thông tin VietQR SePay và cú pháp chuyển khoản
    /// </summary>
    [HttpPost("deposit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DepositResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest request)
    {
        var command = new CreateDepositCommand(CurrentUserId, request.Amount, request.Note);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Rút tiền từ ví về tài khoản ngân hàng của cá nhân (Hoàn tất tức thì, không cần duyệt)
    /// </summary>
    [HttpPost("withdraw")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWithdraw([FromBody] CreateWithdrawRequest request)
    {
        var command = new CreateWithdrawCommand(
            CurrentUserId,
            request.Amount,
            request.DestinationBankCode,
            request.DestinationBankName,
            request.DestinationAccountNumber,
            request.DestinationAccountHolderName,
            request.Note
        );

        var result = await Mediator.Send(command);
        return Ok(result);
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
        var command = new ProcessSepayWebhookCommand(payload, authHeader);
        var result = await Mediator.Send(command);
        return Ok(result);
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
        return Ok(result);
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
        var command = new ApproveWithdrawCommand(id, CurrentUserId, note);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Hủy/Từ chối giao dịch rút tiền và hoàn tiền lại vào ví người dùng
    /// </summary>
    [HttpPost("admin/withdrawals/{id:guid}/reject")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminRejectWithdraw([FromRoute] Guid id, [FromBody] RejectWithdrawRequest request)
    {
        var command = new RejectWithdrawCommand(id, CurrentUserId, request.Reason);
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
