using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Commands.ActivateAuction;
using TrustSimBackend.Application.Features.Auction.Commands.CancelAuction;
using TrustSimBackend.Application.Features.Auction.Commands.UpdateAuction;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Application.Features.Auction.Queries.GetAdminAuctions;
using TrustSimBackend.Application.Features.Auction.Queries.GetAuctionDetail;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý phiên đấu giá — Dành cho Quản trị viên (ADMIN) và Tư vấn viên (CONSULTANT).
/// </summary>
[ApiController]
[Route("api/admin/auctions")]
[Produces("application/json")]
[Authorize(Roles = "ADMIN,CONSULTANT")]
public class AdminAuctionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminAuctionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// [ADMIN/CONSULTANT] Lấy toàn bộ danh sách phiên đấu giá (Mọi trạng thái, có phân trang + lọc).
    /// </summary>
    /// <remarks>
    /// Bộ lọc hỗ trợ:
    /// - `auctionType`: 0 = ENGLISH, 1 = DUTCH
    /// - `status`: 0=DRAFT, 1=SCHEDULE, 2=ACTIVE, 3=END, 4=CANCELLED
    /// - `authorId`: Lọc theo người tạo
    /// - `search`: Tìm theo mã đấu giá hoặc số SIM
    /// - `startFrom` / `startTo`: Lọc theo khoảng thời gian bắt đầu
    /// </remarks>
    [HttpGet]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuctionListItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAllAuctions(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] AuctionType? auctionType = null,
        [FromQuery] AuctionStatus? status = null,
        [FromQuery] Guid? authorId = null,
        [FromQuery] string? search = null,
        [FromQuery] DateTime? startFrom = null,
        [FromQuery] DateTime? startTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAdminAuctionsQuery(
            pageIndex, pageSize, auctionType, status, authorId, search, startFrom, startTo);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [ADMIN/CONSULTANT] Xem chi tiết phiên đấu giá theo ID (bao gồm cả DRAFT/CANCELLED).
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAuctionDetail([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAuctionDetailQuery(AuctionId: id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [ADMIN] Cập nhật thông tin phiên đấu giá (chỉ khi đang SCHEDULE).
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AuctionDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAuction(
        [FromRoute] Guid id,
        [FromBody] UpdateAuctionCommand command,
        CancellationToken cancellationToken)
    {
        var updatedCommand = command with { AuctionId = id };
        updatedCommand.IsAdmin = true;

        var result = await _mediator.Send(updatedCommand, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [ADMIN] Kích hoạt phiên đấu giá: SCHEDULE → ACTIVE.
    /// </summary>
    /// <remarks>
    /// Thường được tự động trigger bởi Background Job đúng giờ `startAt`.
    /// Admin có thể gọi thủ công để kích hoạt sớm nếu cần.
    /// </remarks>
    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateAuction([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ActivateAuctionCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// [ADMIN] Hủy phiên đấu giá (có thể hủy cả khi ACTIVE, kèm lý do).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAuction(
        [FromRoute] Guid id,
        [FromBody] CancelAuctionCommand command,
        CancellationToken cancellationToken)
    {
        var updatedCommand = command with { AuctionId = id };
        updatedCommand.IsAdmin = true;

        var result = await _mediator.Send(updatedCommand, cancellationToken);
        return Ok(result);
    }
}
