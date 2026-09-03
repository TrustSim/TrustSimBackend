using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Commands;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.Queries;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuctionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

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
        var result = await _mediator.Send(query);
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
        var result = await _mediator.Send(new GetAuctionByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// Lấy lịch sử các lượt trả giá (Bids) của một phiên đấu giá
    /// </summary>
    [HttpGet("{id:guid}/bids")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BidDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBids([FromRoute] Guid id, [FromQuery] int top = 50)
    {
        var result = await _mediator.Send(new GetAuctionBidsQuery(id, top));
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new CreateAuctionCommand(
            userId,
            request.SimNumberId,
            request.AuctionType,
            request.StartingPrice,
            request.ReservePrice,
            request.StartAt,
            request.EndAt,
            request.RegistrationStartAt,
            request.RegistrationEndAt
        );

        var result = await _mediator.Send(command);
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var result = await _mediator.Send(new RegisterParticipantCommand(id, userId));
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
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized(ApiResponse<object>.FailureResult("Không xác định được danh tính người dùng từ Token."));
        }

        var command = new PlaceBidCommand(id, userId, request.BidAmount);
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
