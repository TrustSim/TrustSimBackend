using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Commands;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.Queries;
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
        return Ok(result);
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
        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử các lượt trả giá (Bids) của một phiên đấu giá
    /// </summary>
    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BidDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBids([FromRoute] Guid id, [FromQuery] int top = 50)
    {
        var result = await Mediator.Send(new GetAuctionBidsQuery(id, top));
        return Ok(result);
    }

    /// <summary>
    /// Tạo phiên đấu giá mới cho số SIM
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAuctionRequest request)
    {
        var command = new CreateAuctionCommand(
            CurrentUserId,
            request.SimNumberId,
            request.AuctionType,
            request.StartingPrice,
            request.ReservePrice,
            request.StartAt,
            request.EndAt,
            request.RegistrationStartAt,
            request.RegistrationEndAt
        );

        var result = await Mediator.Send(command);
        return Ok(result);
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
        return Ok(result);
    }

    /// <summary>
    /// Trả giá (Đặt Bid) trong phiên đấu giá
    /// </summary>
    [HttpPost("{id:guid}/bids")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<BidDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PlaceBid([FromRoute] Guid id, [FromBody] PlaceBidRequest request)
    {
        var command = new PlaceBidCommand(id, CurrentUserId, request.BidAmount);
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
