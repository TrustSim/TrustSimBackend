using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Commands.CancelAuction;
using TrustSimBackend.Application.Features.Auction.Commands.CreateAuction;
using TrustSimBackend.Application.Features.Auction.Commands.UpdateAuction;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.Queries.GetAuctionDetail;
using TrustSimBackend.Application.Features.Auction.Queries.GetAuctions;
using TrustSimBackend.Application.Features.Auction.Queries.GetMyAuctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý phiên đấu giá SIM — Dành cho Người dùng và Public.
/// </summary>
[ApiController]
[Route("api/auctions")]
[Produces("application/json")]
public class AuctionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuctionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ─────────────────────────────────────────────────────────────
    // PUBLIC: Xem danh sách & tra cứu
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách phiên đấu giá đang hoạt động / đang lên lịch (Phân trang + Lọc đa tiêu chí).
    /// </summary>
    /// <remarks>
    /// **Bộ lọc có thể kết hợp:**
    /// - `auctionType`: 0 = ENGLISH, 1 = DUTCH
    /// - `status`: 1 = SCHEDULE, 2 = ACTIVE
    /// - `carrierId`: Lọc theo nhà mạng
    /// - `categoryId`: Lọc theo danh mục SIM
    /// - `search`: Tìm theo mã đấu giá hoặc số SIM
    /// - `minPrice` / `maxPrice`: Lọc theo khoảng giá hiện tại
    /// - `startFrom` / `startTo`: Lọc theo khoảng thời gian bắt đầu
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuctionListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuctions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] AuctionType? auctionType = null,
        [FromQuery] AuctionStatus? status = null,
        [FromQuery] Guid? carrierId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? search = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] DateTime? startFrom = null,
        [FromQuery] DateTime? startTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAuctionsQuery(pageIndex, pageSize, auctionType, status,
            carrierId, categoryId, search, minPrice, maxPrice, startFrom, startTo);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết phiên đấu giá theo ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionById([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAuctionDetailQuery(AuctionId: id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Tìm kiếm phiên đấu giá theo AuctionCode (VD: AUC-20240801-0001).
    /// </summary>
    [HttpGet("by-code/{code}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionByCode([FromRoute] string code, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAuctionDetailQuery(AuctionCode: code), cancellationToken);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────
    // AUTH: Kho đấu giá của tôi
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách phiên đấu giá do Người dùng hiện tại tạo ra (Kho đấu giá cá nhân).
    /// </summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuctionListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyAuctions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] AuctionStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

        var query = new GetMyAuctionsQuery(pageIndex, pageSize, status) { AuthorId = userId };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────
    // AUTH: Tạo / Sửa / Hủy phiên đấu giá
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo phiên đấu giá mới (Dành cho Người bán - Seller).
    /// </summary>
    /// <remarks>
    /// **English Auction**: yêu cầu `minimumBidIncrement > 0`.<br/>
    /// **Dutch Auction**: yêu cầu `dutchDecreaseAmount > 0` và `dutchDecreaseIntervalSeconds > 0`.<br/><br/>
    /// Phiên được tạo với trạng thái **SCHEDULE** và sẽ tự động **ACTIVE** đúng giờ `startAt`.
    /// </remarks>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAuction(
        [FromBody] CreateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

        command.AuthorId = userId;
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetAuctionById), new { id = result.Data?.AuctionId }, result);
    }

    /// <summary>
    /// Cập nhật thông tin phiên đấu giá (chỉ khi đang SCHEDULE và là người tạo).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAuction(
        [FromRoute] Guid id,
        [FromBody] UpdateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

        // Gán AuctionId từ route (tránh bị giả mạo từ body)
        var updatedCommand = command with { AuctionId = id };
        updatedCommand.RequesterId = userId;
        updatedCommand.IsAdmin = false;

        var result = await _mediator.Send(updatedCommand, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Hủy phiên đấu giá của mình (chỉ khi đang SCHEDULE, chưa bắt đầu).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelMyAuction(
        [FromRoute] Guid id,
        [FromBody] CancelAuctionCommand command,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(ApiResponse<object>.FailureResult("Phiên đăng nhập không hợp lệ hoặc đã hết hạn."));

        var updatedCommand = command with { AuctionId = id };
        updatedCommand.RequesterId = userId;
        updatedCommand.IsAdmin = false;

        var result = await _mediator.Send(updatedCommand, cancellationToken);
        return Ok(result);
    }

    // ─────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrEmpty(claim) && Guid.TryParse(claim, out userId);
    }
}
