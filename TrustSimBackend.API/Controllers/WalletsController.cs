using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

[ApiController]
[Route("api/[controller]")]
public class WalletsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WalletsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy thông tin ví và số dư của tài khoản đang đăng nhập
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyWallet()
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetMyWalletQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách lịch sử nạp / rút tiền của ví cá nhân (có phân trang &amp; bộ lọc)
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var query = new GetWalletTransactionsQuery(userId, type, status, pageIndex, pageSize);
        var result = await _mediator.Send(query);
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new GetWalletTransactionByIdQuery(id, userId));
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu nạp tiền vào ví -&gt; Trả về thông tin VietQR SePay &amp; cú pháp chuyển khoản
    /// </summary>
    [HttpPost("deposit")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DepositResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new CreateDepositCommand(userId, request.Amount, request.Note);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Tạo yêu cầu rút tiền từ ví về tài khoản ngân hàng của người dùng
    /// </summary>
    [HttpPost("withdraw")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<WalletTransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateWithdraw([FromBody] CreateWithdrawRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new CreateWithdrawCommand(
            userId,
            request.Amount,
            request.DestinationBankCode,
            request.DestinationBankName,
            request.DestinationAccountNumber,
            request.DestinationAccountHolderName,
            request.Note
        );

        var result = await _mediator.Send(command);
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
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // ADMIN ENDPOINTS (Duyệt & Quản lý yêu cầu rút tiền)
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Admin] Lấy danh sách tất cả yêu cầu rút tiền trong hệ thống
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
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Phê duyệt yêu cầu rút tiền đã hoàn tất chi tiền
    /// </summary>
    [HttpPost("admin/withdrawals/{id:guid}/approve")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminApproveWithdraw([FromRoute] Guid id, [FromQuery] string? note)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính Admin từ Token."));
        }

        var command = new ApproveWithdrawCommand(id, adminId, note);
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Từ chối yêu cầu rút tiền và hoàn tiền lại vào ví người dùng
    /// </summary>
    [HttpPost("admin/withdrawals/{id:guid}/reject")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminRejectWithdraw([FromRoute] Guid id, [FromBody] RejectWithdrawRequest request)
    {
        if (!TryGetUserId(out var adminId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính Admin từ Token."));
        }

        var command = new RejectWithdrawCommand(id, adminId, request.Reason);
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
