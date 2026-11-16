using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller quản lý tiến trình bàn giao SIM, tiếp nhận bộ bằng chứng sang tên nhà mạng và xác nhận giải ngân Escrow.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SimTransfersController : BaseApiController
{
    /// <summary>
    /// Lấy thông tin chi tiết tiến trình bàn giao SIM và trạng thái giải ngân Escrow của giao dịch.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    [HttpGet("{tradeId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails([FromRoute] Guid tradeId)
    {
        var result = await Mediator.Send(new GetSimTransferDetailsQuery(tradeId, CurrentUserId, IsAdmin));
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, "Lấy thông tin tiến trình bàn giao SIM thành công."));
    }

    /// <summary>
    /// [Bên Bán / Bên Mua / Admin] Lấy chi tiết tiến trình bàn giao SIM dựa theo ID Phiên Đấu Giá (AuctionId).
    /// </summary>
    /// <param name="auctionId">ID của phiên đấu giá SIM.</param>
    [HttpGet("by-auction/{auctionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetailsByAuction([FromRoute] Guid auctionId)
    {
        var result = await Mediator.Send(new GetSimTransferDetailsByAuctionQuery(auctionId, CurrentUserId, IsAdmin));
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, "Lấy thông tin tiến trình bàn giao SIM theo phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Bên Bán / Bên Mua] Nộp bộ bằng chứng gồm nhiều hình ảnh sang tên SIM từ thiết bị (Multipart/Form-Data).
    /// Hỗ trợ IdempotencyKey qua Form hoặc Header (Idempotency-Key / X-Idempotency-Key) để chống nộp trùng.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    /// <param name="request">Danh sách ảnh, loại bằng chứng (SELLER_HANDOVER hoặc BUYER_RECEIPT), mô tả và IdempotencyKey.</param>
    [HttpPost("{tradeId:guid}/proofs")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitMultipleProofs([FromRoute] Guid tradeId, [FromForm] SubmitMultipleTransferProofsFormRequest request)
    {
        var idempotencyKey = ResolveIdempotencyKey(request.IdempotencyKey);
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: CurrentUserId,
            Files: request.Files,
            ProofType: request.ProofType,
            Description: request.Description,
            IdempotencyKey: idempotencyKey
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Nộp bộ bằng chứng thành công."));
    }

    /// <summary>
    /// [Bên Bán / Bên Mua] Nộp bộ bằng chứng gồm nhiều hình ảnh sang tên SIM theo ID Phiên Đấu Giá (AuctionId) (Multipart/Form-Data).
    /// Hỗ trợ IdempotencyKey qua Form hoặc Header (Idempotency-Key / X-Idempotency-Key) để chống nộp trùng.
    /// </summary>
    /// <param name="auctionId">ID của phiên đấu giá SIM.</param>
    /// <param name="request">Danh sách ảnh, loại bằng chứng (SELLER_HANDOVER hoặc BUYER_RECEIPT), mô tả và IdempotencyKey.</param>
    [HttpPost("by-auction/{auctionId:guid}/proofs")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitMultipleProofsByAuction([FromRoute] Guid auctionId, [FromForm] SubmitMultipleTransferProofsFormRequest request)
    {
        var idempotencyKey = ResolveIdempotencyKey(request.IdempotencyKey);
        var command = new SubmitMultipleTransferProofsByAuctionCommand(
            AuctionId: auctionId,
            UserId: CurrentUserId,
            Files: request.Files,
            ProofType: request.ProofType,
            Description: request.Description,
            IdempotencyKey: idempotencyKey
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Nộp bộ bằng chứng theo phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Bên Bán / Bên Mua] Thu hồi (hủy) bộ bằng chứng đang ở trạng thái PENDING để được phép tải lên bộ bằng chứng mới.
    /// </summary>
    /// <param name="transferProofId">ID của bộ bằng chứng (TransferProof) cần thu hồi.</param>
    [HttpPost("proofs/{transferProofId:guid}/withdraw")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> WithdrawProof([FromRoute] Guid transferProofId)
    {
        var result = await Mediator.Send(new WithdrawTransferProofCommand(transferProofId, CurrentUserId));
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Đã thu hồi bộ bằng chứng thành công."));
    }

    /// <summary>
    /// [Admin / Consultant] Lấy danh sách toàn bộ các giao dịch chuyển nhượng đang chờ duyệt hồ sơ bằng chứng sang tên.
    /// </summary>
    /// <param name="auctionId">Lọc theo ID phiên đấu giá (tùy chọn).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo mã phiên, mã giao dịch, số SIM, tên người bán/mua (tùy chọn).</param>
    [HttpGet("admin/pending-reviews")]
    [Authorize(Roles = "ADMIN,Admin,CONSULTANT,Consultant")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingTransferReviewDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviews(
        [FromQuery] Guid? auctionId = null,
        [FromQuery] string? keyword = null)
    {
        var result = await Mediator.Send(new GetPendingTransferReviewsQuery(auctionId, keyword));
        return Ok(ApiResponse<List<PendingTransferReviewDto>>.SuccessResult(result, "Lấy danh sách hồ sơ chờ duyệt thành công."));
    }

    /// <summary>
    /// [Admin / Consultant] Lấy danh sách bộ bằng chứng chuyển nhượng chờ duyệt được gom nhóm theo Phiên đấu giá (Auction).
    /// </summary>
    /// <param name="auctionId">Lọc theo ID phiên đấu giá (tùy chọn).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo mã phiên, mã giao dịch, số SIM, tên người bán/mua (tùy chọn).</param>
    [HttpGet("admin/pending-reviews/by-auction")]
    [Authorize(Roles = "ADMIN,Admin,CONSULTANT,Consultant")]
    [ProducesResponseType(typeof(ApiResponse<List<AuctionPendingTransferReviewGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviewsGroupedByAuction(
        [FromQuery] Guid? auctionId = null,
        [FromQuery] string? keyword = null)
    {
        var result = await Mediator.Send(new GetPendingTransferReviewsGroupedByAuctionQuery(auctionId, keyword));
        return Ok(ApiResponse<List<AuctionPendingTransferReviewGroupDto>>.SuccessResult(result, "Lấy danh sách hồ sơ chờ duyệt gom nhóm theo phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Admin / Consultant] Lấy hồ sơ bằng chứng chuyển nhượng chờ duyệt của một Phiên đấu giá cụ thể.
    /// </summary>
    /// <param name="auctionId">ID của phiên đấu giá.</param>
    [HttpGet("admin/auction/{auctionId:guid}/review")]
    [Authorize(Roles = "ADMIN,Admin,CONSULTANT,Consultant")]
    [ProducesResponseType(typeof(ApiResponse<AuctionPendingTransferReviewGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPendingReviewByAuction([FromRoute] Guid auctionId)
    {
        var result = await Mediator.Send(new GetPendingTransferReviewsGroupedByAuctionQuery(auctionId));
        if (result.Count == 0)
        {
            return NotFound(ApiResponse<object>.FailureResult($"Không tìm thấy hồ sơ bằng chứng chờ duyệt cho phiên đấu giá '{auctionId}'."));
        }

        return Ok(ApiResponse<AuctionPendingTransferReviewGroupDto>.SuccessResult(result[0], "Lấy hồ sơ bằng chứng của phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Admin / Consultant] Duyệt hoặc từ chối trực tiếp một bộ bằng chứng (TransferProof) và toàn bộ ảnh con bên trong.
    /// Khi cả 2 bên đều đã được duyệt hợp lệ, hệ thống tự động niêm phong Văn Bản Số 3 và hoàn tất giao dịch.
    /// </summary>
    /// <param name="transferProofId">ID của bộ bằng chứng cần duyệt/từ chối.</param>
    /// <param name="request">Thông tin phê duyệt (isApproved, rejectionReason).</param>
    [HttpPost("admin/proofs/{transferProofId:guid}/review")]
    [Authorize(Roles = "ADMIN,Admin,CONSULTANT,Consultant")]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReviewProof([FromRoute] Guid transferProofId, [FromBody] ReviewSimTransferProofRequest request)
    {
        var command = new ReviewSimTransferProofCommand(
            TransferProofId: transferProofId,
            AdminUserId: CurrentUserId,
            IsApproved: request.IsApproved,
            RejectionReason: request.RejectionReason
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Đã xử lý duyệt bộ bằng chứng thành công."));
    }

    /// <summary>
    /// [Bên Mua] Xác nhận đã nhận SIM có sóng, hoàn tất chuyển giao quyền sở hữu SIM và giải ngân tiền Escrow cho Người Bán.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    [HttpPost("{tradeId:guid}/confirm")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmTransfer([FromRoute] Guid tradeId)
    {
        var command = new ConfirmSimTransferCommand(
            TradeId: tradeId,
            UserId: CurrentUserId
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Xác nhận nhận SIM và giải ngân thành công."));
    }

    private string? ResolveIdempotencyKey(string? formKey)
    {
        if (!string.IsNullOrWhiteSpace(formKey))
        {
            return formKey.Trim();
        }

        var headers = HttpContext?.Request?.Headers;
        if (headers == null)
        {
            return null;
        }

        if (headers.TryGetValue("Idempotency-Key", out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
        {
            return headerKey.ToString().Trim();
        }

        if (headers.TryGetValue("X-Idempotency-Key", out var xHeaderKey) && !string.IsNullOrWhiteSpace(xHeaderKey))
        {
            return xHeaderKey.ToString().Trim();
        }

        return null;
    }
}
