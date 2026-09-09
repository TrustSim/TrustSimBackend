using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

[Authorize(Roles = "ADMIN")]
public class AuditLogsController : BaseApiController
{
    /// <summary>
    /// [Admin] Tra cứu nhật ký kiểm toán hệ thống (phân trang, lọc theo tên bảng, actor, loại hành động, ngày tạo)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? entityName,
        [FromQuery] Guid? entityId,
        [FromQuery] Guid? actorId,
        [FromQuery] ActionType? actionType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetAuditLogsQuery(entityName, entityId, actorId, actionType, fromDate, toDate, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.SuccessResult(result, "Lấy danh sách nhật ký kiểm toán thành công."));
    }

    /// <summary>
    /// [Admin] Xem chi tiết một bản ghi nhật ký kiểm toán (kèm dữ liệu trước/sau thay đổi - OldData vs NewData)
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AuditLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetAuditLogByIdQuery(id));
        return Ok(ApiResponse<AuditLogDto>.SuccessResult(result, "Lấy thông tin chi tiết nhật ký kiểm toán thành công."));
    }

    /// <summary>
    /// [Admin] Xuất nhật ký kiểm toán ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string? entityName,
        [FromQuery] ActionType? actionType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate)
    {
        var query = new ExportAuditLogsQuery(entityName, actionType, fromDate, toDate);
        var bytes = await Mediator.Send(query);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"AuditLogs_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }
}
