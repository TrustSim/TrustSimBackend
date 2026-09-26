using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class AuctionsController : BaseApiController
{
    /// <summary>
    /// Tìm kiếm và lọc danh sách phiên đấu giá (phân trang, lọc theo trạng thái, loại hình, từ khóa)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuctionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuctions(
        [FromQuery] string? keyword,
        [FromQuery] AuctionStatus? status,
        [FromQuery] AuctionType? type,
        [FromQuery] Guid? authorId,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetAuctionsQuery(keyword, status, type, authorId, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<AuctionDto>>.SuccessResult(result, "Lấy danh sách phiên đấu giá thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết phiên đấu giá
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuctionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetAuctionByIdQuery(id));
        return Ok(ApiResponse<AuctionDto>.SuccessResult(result, "Lấy thông tin phiên đấu giá thành công."));
    }

    /// <summary>
    /// Lấy lịch sử các lượt trả giá (Bids) của một phiên đấu giá
    /// </summary>
    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BidDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBids([FromRoute] Guid id, [FromQuery] int top = 50)
    {
        var result = await Mediator.Send(new GetAuctionBidsQuery(id, top));
        return Ok(ApiResponse<IReadOnlyList<BidDto>>.SuccessResult(result, "Lấy lịch sử trả giá thành công."));
    }

    /// <summary>
    /// Tạo phiên đấu giá mới cho số SIM
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAuctionCommand command)
    {
        var result = await Mediator.Send(command with { AuthorId = CurrentUserId });
        return Ok(ApiResponse<AuctionDto>.SuccessResult(result, "Tạo phiên đấu giá thành công."));
    }

    /// <summary>
    /// Đăng ký tham gia phiên đấu giá
    /// </summary>
    [HttpPost("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterParticipant([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new RegisterParticipantCommand(id, CurrentUserId));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đăng ký tham gia phiên đấu giá thành công."));
    }

    /// <summary>
    /// Trả giá (Đặt Bid) trong phiên đấu giá kiểu Anh (English Auction)
    /// </summary>
    [HttpPost("{id:guid}/bids")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BidDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlaceBid([FromRoute] Guid id, [FromBody] PlaceBidCommand command)
    {
        var result = await Mediator.Send(command with { AuctionId = id, UserId = CurrentUserId });
        return Ok(ApiResponse<BidDto>.SuccessResult(result, "Trả giá thành công."));
    }

    /// <summary>
    /// Chấp nhận mua ngay (Instant Buy) trong phiên đấu giá kiểu Hà Lan (Dutch Auction)
    /// </summary>
    [HttpPost("{id:guid}/dutch-buy")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptDutchBuy([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new AcceptDutchBuyCommand(id, CurrentUserId));
        return Ok(ApiResponse<AuctionResultDto>.SuccessResult(result, "Chấp nhận mua SIM thành công."));
    }

    /// <summary>
    /// Người thắng hoàn tất thanh toán số tiền còn lại trong thời hạn quy định
    /// </summary>
    [HttpPost("{id:guid}/confirm-payment")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ConfirmPayment([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new ConfirmWinnerPaymentCommand(id, CurrentUserId));
        return Ok(ApiResponse<AuctionResultDto>.SuccessResult(result, "Thanh toán phiên đấu giá thành công."));
    }

    /// <summary>
    /// Người về nhì chấp nhận mua SIM với mức giá đặt cao nhất của họ (sau khi Người thắng bùng cọc)
    /// </summary>
    [HttpPost("{id:guid}/runner-up/accept")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptRunnerUpOffer([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new AcceptRunnerUpOfferCommand(id, CurrentUserId));
        return Ok(ApiResponse<AuctionResultDto>.SuccessResult(result, "Chấp nhận mua SIM thành công."));
    }

    /// <summary>
    /// Người về nhì từ chối mua SIM khi được đề nghị chuyển quyền
    /// </summary>
    [HttpPost("{id:guid}/runner-up/reject")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectRunnerUpOffer([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new RejectRunnerUpOfferCommand(id, CurrentUserId));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Từ chối nhận quyền mua SIM thành công."));
    }

    /// <summary>
    /// [Admin] Hủy phiên đấu giá do vi phạm quy chế hoặc gian lận
    /// </summary>
    [HttpPatch("{id:guid}/cancel")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AdminCancelAuction([FromRoute] Guid id, [FromQuery] string reason)
    {
        var result = await Mediator.Send(new AdminCancelAuctionCommand(id, reason, CurrentUserId));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Hủy phiên đấu giá thành công."));
    }

    /// <summary>
    /// [Admin] Xuất báo cáo danh sách phiên đấu giá ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminExportAuctions(
        [FromQuery] string? keyword,
        [FromQuery] AuctionStatus? status,
        [FromQuery] AuctionType? type)
    {
        var bytes = await Mediator.Send(new AdminExportAuctionsQuery(keyword, status, type));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Auctions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }
}

