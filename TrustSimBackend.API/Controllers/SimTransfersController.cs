using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Controller quản lý tiến trình bàn giao SIM, tiếp nhận biên bản sang tên nhà mạng và xác nhận giải ngân Escrow.
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
    /// [Bên Bán] Nộp ảnh bằng chứng/biên bản chuyển nhượng SIM từ điểm giao dịch nhà mạng (URL đơn lẻ).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    /// <param name="request">Chứa đường dẫn ảnh biên bản và mô tả.</param>
    [HttpPost("{tradeId:guid}/proof")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitProof([FromRoute] Guid tradeId, [FromBody] SubmitProofRequest request)
    {
        var command = new SubmitTransferProofCommand(
            TradeId: tradeId,
            UserId: CurrentUserId,
            ImageUrl: request.ImageUrl,
            Description: request.Description
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Nộp biên bản sang tên thành công."));
    }

    /// <summary>
    /// [Bên Bán / Bên Mua] Nộp nhiều tệp hình ảnh bằng chứng sang tên SIM từ thiết bị (Multipart/Form-Data).
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    /// <param name="request">Chứa danh sách files ảnh, loại bằng chứng (SELLER_HANDOVER hoặc BUYER_RECEIPT) và mô tả.</param>
    [HttpPost("{tradeId:guid}/proofs")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitMultipleProofs([FromRoute] Guid tradeId, [FromForm] SubmitMultipleTransferProofsFormRequest request)
    {
        var command = new SubmitMultipleTransferProofsCommand(
            TradeId: tradeId,
            UserId: CurrentUserId,
            Files: request.Files,
            ProofType: request.ProofType,
            Description: request.Description
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Nộp danh sách bằng chứng thành công."));
    }

    /// <summary>
    /// [Admin] Lấy danh sách toàn bộ các giao dịch chuyển nhượng đang chờ duyệt hồ sơ bằng chứng sang tên.
    /// </summary>
    /// <param name="auctionId">Lọc theo ID phiên đấu giá (tùy chọn).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo mã phiên, mã giao dịch, số SIM, tên người bán/mua (tùy chọn).</param>
    [HttpGet("admin/pending-reviews")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<PendingTransferReviewDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviews(
        [FromQuery] Guid? auctionId = null,
        [FromQuery] string? keyword = null)
    {
        var result = await Mediator.Send(new GetPendingTransferReviewsQuery(auctionId, keyword));
        return Ok(ApiResponse<List<PendingTransferReviewDto>>.SuccessResult(result, "Lấy danh sách hồ sơ chờ duyệt thành công."));
    }

    /// <summary>
    /// [Admin] Lấy danh sách hồ sơ bằng chứng chuyển nhượng chờ duyệt được gom nhóm theo Phiên đấu giá (Auction).
    /// </summary>
    /// <param name="auctionId">Lọc theo ID phiên đấu giá (tùy chọn).</param>
    /// <param name="keyword">Từ khóa tìm kiếm theo mã phiên, mã giao dịch, số SIM, tên người bán/mua (tùy chọn).</param>
    [HttpGet("admin/pending-reviews/by-auction")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<List<AuctionPendingTransferReviewGroupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReviewsGroupedByAuction(
        [FromQuery] Guid? auctionId = null,
        [FromQuery] string? keyword = null)
    {
        var result = await Mediator.Send(new GetPendingTransferReviewsGroupedByAuctionQuery(auctionId, keyword));
        return Ok(ApiResponse<List<AuctionPendingTransferReviewGroupDto>>.SuccessResult(result, "Lấy danh sách hồ sơ chờ duyệt gom nhóm theo phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Admin] Lấy hồ sơ bằng chứng chuyển nhượng chờ duyệt của một Phiên đấu giá cụ thể.
    /// </summary>
    /// <param name="auctionId">ID của phiên đấu giá.</param>
    [HttpGet("admin/auction/{auctionId:guid}/review")]
    [Authorize(Roles = "ADMIN,Admin")]
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
    /// [Admin] Duyệt hoặc từ chối hồ sơ bằng chứng sang tên của Bên Bán hoặc Bên Mua.
    /// Khi cả 2 bên đều đã được duyệt hợp lệ, hệ thống tự động niêm phong Văn Bản Số 3 và hoàn tất giao dịch.
    /// </summary>
    /// <param name="tradeId">ID của giao dịch chuyển nhượng SIM.</param>
    /// <param name="request">Thông tin phê duyệt (targetParty, isApproved, rejectionReason).</param>
    [HttpPost("admin/{tradeId:guid}/review")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<SimTransferDetailsDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewProof([FromRoute] Guid tradeId, [FromBody] ReviewSimTransferProofRequest request)
    {
        var command = new ReviewSimTransferProofCommand(
            TradeId: tradeId,
            AdminUserId: CurrentUserId,
            TargetParty: request.TargetParty,
            IsApproved: request.IsApproved,
            RejectionReason: request.RejectionReason
        );

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimTransferDetailsDto>.SuccessResult(result, result.Message ?? "Đã xử lý duyệt hồ sơ thành công."));
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
}
