using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller quản lý quy trình xem trước, cấp phát OTP và ký số hợp đồng điện tử chuyển nhượng SIM.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ContractsController : BaseApiController
{
    /// <summary>
    /// Xem trước nội dung hợp đồng chuyển nhượng SIM 3 bên (dạng HTML) và trạng thái ký của Bên Mua / Bên Bán.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpGet("{tradeId:guid}/preview")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ContractPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContractPreview([FromRoute] Guid tradeId)
    {
        var result = await Mediator.Send(new GetContractPreviewQuery(tradeId, CurrentUserId, IsAdmin));
        return Ok(ApiResponse<ContractPreviewDto>.SuccessResult(result, "Lấy nội dung xem trước hợp đồng thành công."));
    }

    /// <summary>
    /// Tải tệp tin hợp đồng PDF chuyển nhượng quyền sử dụng SIM (Chuyển hướng trực tiếp tới CDN Cloud Supabase Storage).
    /// Yêu cầu xác thực tài khoản có thẩm quyền (Bên Mua, Bên Bán hoặc Quản trị viên sàn) qua Header hoặc Query ?token=...
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpGet("{tradeId:guid}/download")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadContractPdf([FromRoute] Guid tradeId)
    {
        var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var trade = await unitOfWork.TradeTransactions.GetByIdAsync(tradeId);
        if (trade == null)
        {
            return NotFound(ApiResponse<object>.FailureResult($"Không tìm thấy giao dịch chuyển nhượng có ID '{tradeId}'."));
        }

        var isBuyer = trade.BuyerId == CurrentUserId;
        var isSeller = trade.SellerId == CurrentUserId;

        if (!isBuyer && !isSeller && !IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse<object>.FailureResult("Bạn không có quyền tải hoặc xem hợp đồng của giao dịch này."));
        }

        var docUrl = trade.TransferContract?.DocumentUrl;
        if (string.IsNullOrWhiteSpace(docUrl))
        {
            return NotFound(ApiResponse<object>.FailureResult("Hợp đồng chưa được niêm phong hoặc tệp PDF chưa sẵn sàng."));
        }

        if (docUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) 
            || docUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(docUrl);
        }

        return NotFound(ApiResponse<object>.FailureResult("Đường dẫn tệp hợp đồng không hợp lệ."));
    }

    /// <summary>
    /// Tải tệp tin Phiếu xác nhận kết quả đấu giá / Phiếu chốt giá trúng điện tử (Văn bản 1) từ CDN Cloud Supabase Storage.
    /// Yêu cầu xác thực tài khoản có thẩm quyền (Bên Mua, Bên Bán hoặc Quản trị viên sàn).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpGet("{tradeId:guid}/winning-notice")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadWinningNoticePdf([FromRoute] Guid tradeId)
    {
        var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var trade = await unitOfWork.TradeTransactions.GetByIdAsync(tradeId);
        if (trade == null)
        {
            return NotFound(ApiResponse<object>.FailureResult($"Không tìm thấy giao dịch chuyển nhượng có ID '{tradeId}'."));
        }

        var isBuyer = trade.BuyerId == CurrentUserId;
        var isSeller = trade.SellerId == CurrentUserId;

        if (!isBuyer && !isSeller && !IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse<object>.FailureResult("Bạn không có quyền tải hoặc xem phiếu chốt giá trúng của giao dịch này."));
        }

        var noticeUrl = trade.TransferContract?.WinningNoticeUrl;
        if (string.IsNullOrWhiteSpace(noticeUrl))
        {
            var preview = await Mediator.Send(new GetContractPreviewQuery(tradeId, CurrentUserId, IsAdmin));
            noticeUrl = preview.WinningNoticeUrl;
        }

        if (string.IsNullOrWhiteSpace(noticeUrl))
        {
            return NotFound(ApiResponse<object>.FailureResult("Phiếu chốt giá trúng điện tử chưa sẵn sàng hoặc giao dịch không hợp lệ."));
        }

        if (noticeUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) 
            || noticeUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(noticeUrl);
        }

        return NotFound(ApiResponse<object>.FailureResult("Đường dẫn tệp phiếu chốt giá trúng không hợp lệ."));
    }

    /// <summary>
    /// Xem trước nội dung Biên bản nghiệm thu và thanh lý giao dịch điện tử (Văn bản 3 dạng HTML).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpGet("{tradeId:guid}/liquidation/preview")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LiquidationPreviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLiquidationPreview([FromRoute] Guid tradeId)
    {
        var result = await Mediator.Send(new GetLiquidationPreviewQuery(tradeId, CurrentUserId, IsAdmin));
        return Ok(ApiResponse<LiquidationPreviewDto>.SuccessResult(result, "Lấy nội dung xem trước biên bản thanh lý thành công."));
    }

    /// <summary>
    /// Tải tệp tin Biên bản nghiệm thu và thanh lý giao dịch điện tử (Văn bản 3) đã niêm phong từ CDN Supabase Storage.
    /// Yêu cầu xác thực tài khoản có thẩm quyền (Bên Mua, Bên Bán hoặc Quản trị viên sàn).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpGet("{tradeId:guid}/liquidation/download")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadLiquidationPdf([FromRoute] Guid tradeId)
    {
        var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var trade = await unitOfWork.TradeTransactions.GetByIdAsync(tradeId);
        if (trade == null)
        {
            return NotFound(ApiResponse<object>.FailureResult($"Không tìm thấy giao dịch chuyển nhượng có ID '{tradeId}'."));
        }

        var isBuyer = trade.BuyerId == CurrentUserId;
        var isSeller = trade.SellerId == CurrentUserId;

        if (!isBuyer && !isSeller && !IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse<object>.FailureResult("Bạn không có quyền tải hoặc xem biên bản thanh lý của giao dịch này."));
        }

        var liqUrl = trade.TransferContract?.LiquidationDocumentUrl;
        if (string.IsNullOrWhiteSpace(liqUrl))
        {
            return NotFound(ApiResponse<object>.FailureResult("Biên bản nghiệm thu và thanh lý chưa được niêm phong hoặc tệp chưa sẵn sàng."));
        }

        if (liqUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) 
            || liqUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(liqUrl);
        }

        return NotFound(ApiResponse<object>.FailureResult("Đường dẫn tệp biên bản thanh lý không hợp lệ."));
    }

    /// <summary>
    /// Gửi mã xác thực OTP qua Email cho Bên Mua hoặc Bên Bán để phục vụ ký hợp đồng.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    [HttpPost("{tradeId:guid}/send-otp")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SendOtpResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendSigningOtp([FromRoute] Guid tradeId)
    {
        var result = await Mediator.Send(new SendContractSigningOtpCommand(tradeId, CurrentUserId));
        return Ok(ApiResponse<SendOtpResponseDto>.SuccessResult(result, "Mã OTP ký hợp đồng đã được gửi đến email của bạn."));
    }

    /// <summary>
    /// Ký xác nhận hợp đồng điện tử bằng mã OTP Email (kèm lưu vết kiểm toán IP, User-Agent, SHA-256 OTP).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    /// <param name="request">Chứa mã OTP 6 chữ số.</param>
    [HttpPost("{tradeId:guid}/sign")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SignContractResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SignContract([FromRoute] Guid tradeId, [FromBody] SignContractRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var userAgent = Request.Headers.UserAgent.ToString();

        var command = new SignContractWithOtpCommand(
            TradeId: tradeId,
            UserId: CurrentUserId,
            OtpCode: request.OtpCode,
            IpAddress: ipAddress,
            UserAgent: userAgent);

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SignContractResultDto>.SuccessResult(result, result.Message));
    }

    /// <summary>
    /// Kiểm định tính toàn vẹn và chữ ký số PAdES X.509 của tệp tin hợp đồng PDF.
    /// Đối chiếu mã SHA-256 thực tế của file PDF với mã lưu trong CSDL đã được niêm phong.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng.</param>
    /// <param name="request">Chứa mã hash SHA-256 do client tính từ file PDF.</param>
    [HttpPost("{tradeId:guid}/verify-integrity")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<VerifyContractIntegrityResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyIntegrity([FromRoute] Guid tradeId, [FromBody] VerifyContractIntegrityRequest request)
    {
        var unitOfWork = HttpContext.RequestServices.GetRequiredService<IUnitOfWork>();
        var trade = await unitOfWork.TradeTransactions.GetByIdAsync(tradeId);
        if (trade == null)
        {
            return NotFound(ApiResponse<object>.FailureResult("Không tìm thấy giao dịch chuyển nhượng."));
        }

        var contract = trade.TransferContract;
        if (contract == null || contract.ContractStatus != ContractStatus.FULLY_SIGNED)
        {
            return Ok(ApiResponse<VerifyContractIntegrityResponse>.SuccessResult(new VerifyContractIntegrityResponse(
                IsValid: false,
                DocumentStatus: contract?.ContractStatus.ToString() ?? "NONE",
                ExpectedDocumentHash: contract?.DocumentHash,
                ProvidedSha256: request.ClientSha256,
                HashMatched: false,
                PadesSignatureSerial: null,
                PadesSignatureHash: null,
                TsaTimestamp: null,
                TsaAuthority: null,
                SealedAt: null,
                IsAutoEnforced: false,
                TotalSignatures: contract?.DigitalSignatures.Count ?? 0,
                Message: "Hợp đồng chưa được niêm phong hoàn tất chữ ký số PAdES X.509."
            ), "Hợp đồng chưa được niêm phong hoàn tất."));
        }

        var expectedHash = contract.DocumentHash?.Trim().ToLowerInvariant() ?? string.Empty;
        var clientHash = request.ClientSha256?.Trim().ToLowerInvariant() ?? string.Empty;
        var isHashMatch = !string.IsNullOrEmpty(clientHash) && string.Equals(expectedHash, clientHash, StringComparison.OrdinalIgnoreCase);

        var isValid = isHashMatch && !string.IsNullOrEmpty(contract.PadesSignatureSerial);
        var message = isValid
            ? "✅ HỢP ĐỒNG NGUYÊN BẢN TUYỆT ĐỐI! Mã hash SHA-256 hoàn toàn trùng khớp và chữ ký số PAdES X.509 / Dấu thời gian TSA hợp lệ."
            : (!string.IsNullOrEmpty(clientHash) && !isHashMatch)
                ? "🚨 CẢNH BÁO VI PHẠM: Tệp tin hợp đồng đã bị chỉnh sửa hoặc can thiệp! Mã băm SHA-256 không khớp với bản niêm phong gốc."
                : "⚠️ Cần cung cấp mã hash SHA-256 của file để đối chiếu toàn vẹn.";

        var response = new VerifyContractIntegrityResponse(
            IsValid: isValid,
            DocumentStatus: contract.ContractStatus.ToString(),
            ExpectedDocumentHash: contract.DocumentHash,
            ProvidedSha256: request.ClientSha256,
            HashMatched: isHashMatch,
            PadesSignatureSerial: contract.PadesSignatureSerial,
            PadesSignatureHash: contract.PadesSignatureHash,
            TsaTimestamp: contract.TsaTimestamp,
            TsaAuthority: contract.TsaAuthority,
            SealedAt: contract.SealedAt,
            IsAutoEnforced: contract.IsAutoEnforced,
            TotalSignatures: contract.DigitalSignatures.Count,
            Message: message
        );

        return Ok(ApiResponse<VerifyContractIntegrityResponse>.SuccessResult(response, message));
    }
}
