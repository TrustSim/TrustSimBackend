using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

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
        var query = new GetMyNotificationsQuery(CurrentUserId, isRead, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<NotificationDto>>.SuccessResult(result, "Lấy danh sách thông báo thành công."));
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
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đánh dấu đã đọc thông báo thành công."));
    }

    /// <summary>
    /// [Admin] Phát thông báo toàn sàn (Broadcast) hoặc gửi thông báo theo nhóm vai trò
    /// </summary>
    [HttpPost("broadcast")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastNotificationCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<int>.SuccessResult(result, $"Đã gửi thông báo thành công đến {result} người dùng."));
    }
}

