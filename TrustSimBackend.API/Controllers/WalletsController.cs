using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.Commands;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Application.Features.Financial.Queries;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

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
    public async Task<IActionResult> GetTransactions([FromQuery] WalletTransactionQueryFilter filter)
    {
        var result = await Mediator.Send(new GetWalletTransactionsQuery(CurrentUserId, filter));
        return HandlePagedResult(result, "Lấy lịch sử giao dịch thành công.");
    }

    /// <summary>
    /// Lấy sao kê tài chính tổng hợp (kết hợp cả giao dịch nạp/rút ví và các biến động cọc/Escrow/chuyển nhượng SIM)
    /// </summary>
    [HttpGet("statement")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UnifiedWalletTransactionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetUnifiedStatement([FromQuery] UnifiedStatementQueryFilter filter)
    {
        var result = await Mediator.Send(new GetUnifiedWalletStatementQuery(CurrentUserId, filter));
        return HandlePagedResult(result, "Lấy sao kê tài chính tổng hợp thành công.");
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
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositRequest request)
    {
        var idempotencyKey = request.IdempotencyKey
            ?? (Request.Headers.TryGetValue("Idempotency-Key", out var headerKey) ? headerKey.ToString() : null)
            ?? (Request.Headers.TryGetValue("X-Idempotency-Key", out var xHeaderKey) ? xHeaderKey.ToString() : null);

        var command = new CreateDepositCommand(request.Amount, request.Note, CurrentUserId, idempotencyKey);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<DepositResponseDto>.SuccessResult(result, "Tạo yêu cầu nạp tiền thành công. Vui lòng thanh toán theo thông tin hướng dẫn."));
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
        var idempotencyKey = request.IdempotencyKey
            ?? (Request.Headers.TryGetValue("Idempotency-Key", out var headerKey) ? headerKey.ToString() : null)
            ?? (Request.Headers.TryGetValue("X-Idempotency-Key", out var xHeaderKey) ? xHeaderKey.ToString() : null);

        var command = new CreateWithdrawCommand(
            request.Amount,
            request.DestinationBankCode,
            request.DestinationBankName,
            request.DestinationAccountNumber,
            request.DestinationAccountHolderName,
            request.Note,
            CurrentUserId,
            idempotencyKey);

        var result = await Mediator.Send(command);
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
    public async Task<IActionResult> AdminGetWithdrawals([FromQuery] AdminWithdrawalQueryFilter filter)
    {
        var result = await Mediator.Send(new AdminGetWithdrawalsQuery(filter));
        return HandlePagedResult(result, "Lấy danh sách yêu cầu rút tiền thành công.");
    }

    /// <summary>
    /// [Admin] Lấy danh sách ví người dùng và số dư (phân trang, tìm kiếm theo username/email/tên, lọc trạng thái ví)
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AdminWalletDetailDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetWallets([FromQuery] AdminWalletQueryFilter filter)
    {
        var result = await Mediator.Send(new AdminGetWalletsQuery(filter));
        return HandlePagedResult(result, "Lấy danh sách ví thành công.");
    }

    /// <summary>
    /// [Admin] Đóng băng hoặc mở đóng băng ví người dùng
    /// </summary>
    [HttpPatch("admin/{id:guid}/freeze")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminFreezeWallet([FromRoute] Guid id, [FromBody] AdminFreezeWalletRequest request)
    {
        var result = await Mediator.Send(new AdminFreezeWalletCommand(id, request.Freeze));
        var msg = request.Freeze ? "Đã đóng băng ví người dùng thành công." : "Đã mở đóng băng ví người dùng thành công.";
        return Ok(ApiResponse<bool>.SuccessResult(result, msg));
    }

    /// <summary>
    /// [Admin] Điều chỉnh số dư ví thủ công (cộng hoặc trừ số dư, ghi nhận log kế toán)
    /// </summary>
    [HttpPost("admin/{id:guid}/adjust-balance")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminAdjustBalance([FromRoute] Guid id, [FromBody] AdminAdjustBalanceRequest request)
    {
        var command = new AdminAdjustBalanceCommand(id, request.Amount, request.Reason, CurrentUserId);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Điều chỉnh số dư ví thành công."));
    }

    /// <summary>
    /// [Admin] Tra cứu toàn bộ lịch sử biến động số dư trên toàn sàn
    /// </summary>
    [HttpGet("admin/transactions")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetAllTransactions([FromQuery] AdminWalletTransactionsFilter filter)
    {
        var result = await Mediator.Send(new AdminGetAllTransactionsQuery(filter));
        return HandlePagedResult(result, "Lấy lịch sử giao dịch toàn sàn thành công.");
    }

    /// <summary>
    /// [Payment Intent] Tạo yêu cầu nạp tiền chuẩn Idempotent Intent -> Trả về thông tin VietQR và trạng thái Intent
    /// </summary>
    [HttpPost("deposit-intent")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaymentIntentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDepositIntent([FromBody] CreateDepositIntentRequest request)
    {
        var command = new CreateDepositIntentCommand(CurrentUserId, request.Amount, request.IdempotencyKey);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<PaymentIntentDto>.SuccessResult(result, "Tạo Payment Intent nạp tiền thành công."));
    }

    /// <summary>
    /// [Admin] Quét và kiểm tra tính toàn vẹn chuỗi khối Hash Chain SHA-256 &amp; Chữ ký số ECDsa thời gian thực
    /// </summary>
    [HttpPost("admin/ledger/verify-chain")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<LedgerIntegrityReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyLedgerChain([FromQuery] long fromSequence = 1, [FromQuery] int limit = 10000)
    {
        var result = await Mediator.Send(new VerifyLedgerChainCommand(fromSequence, limit));
        var msg = result.IsChainValid && result.IsSignatureValid
            ? "Chuỗi khối Sổ cái BẢO TOÀN TOÀN VẸN 100% (Green - Valid)."
            : "CẢNH BÁO: Phát hiện dấu hiệu đứt gãy hoặc dữ liệu sổ cái đã bị chỉnh sửa trái phép (Red - Tamper Detected)!";
        return Ok(ApiResponse<LedgerIntegrityReportDto>.SuccessResult(result, msg));
    }

    /// <summary>
    /// [Admin] Báo cáo đối soát tài chính toàn sàn (Sổ cái kép vs Materialized Balance tất cả ví người dùng)
    /// </summary>
    [HttpGet("admin/ledger/audit")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<LedgerIntegrityReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedgerAuditReport()
    {
        var result = await Mediator.Send(new GetLedgerIntegrityReportQuery());
        return Ok(ApiResponse<LedgerIntegrityReportDto>.SuccessResult(result, "Lấy báo cáo kiểm toán toàn vẹn tài chính thành công."));
    }

    /// <summary>
    /// [Admin] Tra cứu danh sách chứng từ kế toán ghi kép (phân trang và bộ lọc)
    /// </summary>
    [HttpGet("admin/ledger/journals")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LedgerJournalDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLedgerJournals([FromQuery] LedgerJournalQueryFilter filter)
    {
        var result = await Mediator.Send(new GetLedgerJournalsQuery(filter));
        return HandlePagedResult(result, "Lấy danh sách chứng từ kế toán thành công.");
    }

    /// <summary>
    /// [Admin] Xem chi tiết một chứng từ kế toán và toàn bộ các dòng Nợ / Có kèm Hash &amp; Chữ ký số
    /// </summary>
    [HttpGet("admin/ledger/journals/{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<LedgerJournalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLedgerJournalDetail([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetLedgerJournalDetailQuery(id));
        return Ok(ApiResponse<LedgerJournalDto>.SuccessResult(result, "Lấy chi tiết chứng từ kế toán thành công."));
    }

    /// <summary>
    /// [Admin] Demo Hội đồng: Mô phỏng hành vi DBA cố tình sửa 1 byte SQL và kiểm chứng hệ thống báo động đỏ tức thì
    /// </summary>
    [HttpPost("admin/ledger/simulate-tamper-test")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<TamperSimulationResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SimulateTamperTest([FromBody] SimulateTamperRequest request)
    {
        var command = new SimulateTamperTestCommand(request.SequenceNumber, request.TamperedAmount);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TamperSimulationResultDto>.SuccessResult(result, "Mô phỏng can thiệp dữ liệu và kiểm tra toàn vẹn hoàn tất."));
    }

    /// <summary>
    /// [Admin] Xuất lịch sử biến động số dư ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("admin/transactions/export")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminExportTransactions([FromQuery] AdminWalletTransactionsFilter filter)
    {
        var bytes = await Mediator.Send(new AdminExportTransactionsQuery(filter));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"WalletTransactions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// [Admin] Báo cáo tổng quan tài chính sàn: Tổng tiền ngân hàng, nợ khách hàng (ví, cọc, Escrow) và lợi nhuận khả dụng có thể rút
    /// </summary>
    [HttpGet("admin/financial/overview")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AdminFinancialOverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetFinancialOverview()
    {
        var result = await Mediator.Send(new GetAdminFinancialOverviewQuery());
        return Ok(ApiResponse<AdminFinancialOverviewDto>.SuccessResult(result, "Lấy thông tin tổng quan tài chính sàn thành công."));
    }

    /// <summary>
    /// [Admin] Rút tiền lợi nhuận sàn về tài khoản ngân hàng cá nhân của Ban Quản Trị
    /// </summary>
    [HttpPost("admin/financial/withdraw-profit")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AdminProfitWithdrawResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AdminWithdrawProfit([FromBody] AdminWithdrawProfitRequest request)
    {
        var idempotencyKey = request.IdempotencyKey
            ?? (Request.Headers.TryGetValue("Idempotency-Key", out var headerKey) ? headerKey.ToString() : null)
            ?? (Request.Headers.TryGetValue("X-Idempotency-Key", out var xHeaderKey) ? xHeaderKey.ToString() : null);

        var command = new AdminWithdrawProfitCommand(
            request.Amount,
            request.DestinationBankCode,
            request.DestinationBankName,
            request.DestinationAccountNumber,
            request.DestinationAccountHolderName,
            request.Note,
            CurrentUserId,
            idempotencyKey);

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<AdminProfitWithdrawResultDto>.SuccessResult(result, "Rút tiền lợi nhuận sàn thành công."));
    }

    /// <summary>
    /// [Admin] Tra cứu lịch sử biến động doanh thu sàn (hoa hồng bán SIM, tiền phạt bỏ cọc, các lần rút lợi nhuận)
    /// </summary>
    [HttpGet("admin/financial/revenue-history")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PlatformRevenueHistoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminGetRevenueHistory([FromQuery] RevenueHistoryQueryFilter filter)
    {
        var result = await Mediator.Send(new GetRevenueHistoryQuery(filter));
        return HandlePagedResult(result, "Lấy lịch sử biến động doanh thu sàn thành công.");
    }
}


