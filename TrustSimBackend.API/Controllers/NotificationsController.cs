using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.Commands;
using TrustSimBackend.Application.Features.System.DTOs;
using TrustSimBackend.Application.Features.System.Queries;

namespace TrustSimBackend.API.Controllers;

public class NotificationsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách thông báo của người dùng hiện tại
    /// </summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyNotifications(
        [FromQuery] bool? isRead,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetMyNotificationsQuery(CurrentUserId, isRead, pageIndex, pageSize));
        return Ok(result);
    }

    /// <summary>
    /// Đánh dấu thông báo đã đọc
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new MarkNotificationAsReadCommand(id, CurrentUserId));
        return Ok(result);
    }
}
