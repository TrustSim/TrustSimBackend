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
    /// Trả giá (Đặt Bid) trong phiên đấu giá
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
}
