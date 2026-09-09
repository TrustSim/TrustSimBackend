using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.API.Controllers;

public class SimsController : BaseApiController
{
    /// <summary>
    /// Tìm kiếm và lọc danh sách SIM số đẹp (phân trang, lọc theo nhà mạng, thể loại, đầu số, từ khóa)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimNumberDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSims(
        [FromQuery] string? keyword,
        [FromQuery] Guid? carrierId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? prefixId,
        [FromQuery] SimStatus? status,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = new GetSimsQuery(keyword, carrierId, categoryId, prefixId, status, pageIndex, pageSize);
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<SimNumberDto>>.SuccessResult(result, "Lấy danh sách SIM thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một số SIM
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetSimByIdQuery(id));
        return Ok(ApiResponse<SimNumberDto>.SuccessResult(result, "Lấy thông tin chi tiết số SIM thành công."));
    }

    /// <summary>
    /// [Admin/Consultant] Thêm mới số SIM vào kho
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSimCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimNumberDto>.SuccessResult(result, "Thêm mới số SIM thành công."));
    }

    /// <summary>
    /// [Admin/Consultant] Cập nhật thông tin và trạng thái số SIM
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN,CONSULTANT")]
    [ProducesResponseType(typeof(ApiResponse<SimNumberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateSimCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<SimNumberDto>.SuccessResult(result, "Cập nhật số SIM thành công."));
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) số SIM khỏi hệ thống
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteSimCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa số SIM thành công."));
    }

    /// <summary>
    /// [Người bán/Seller] Bulk Import danh sách SIM số đẹp sở hữu từ file Excel (.xlsx) hoặc CSV (.csv) bằng EFCore.BulkExtensions
    /// </summary>
    [HttpPost("import")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<SimNumberDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Import([FromForm] ImportSimsRequest request)
    {
        var result = await Mediator.Send(new ImportSimsCommand(request.File, CurrentUserId, AutoVerify: false));
        return Ok(ApiResponse<ImportResult<SimNumberDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} SIM hợp lệ đã được nạp vào kho."));
    }

    /// <summary>
    /// Xuất danh sách SIM số đẹp theo bộ lọc ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string? keyword,
        [FromQuery] Guid? carrierId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? prefixId,
        [FromQuery] SimStatus? status)
    {
        var query = new ExportSimsQuery(keyword, carrierId, categoryId, prefixId, status);
        var bytes = await Mediator.Send(query);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SimNumbers_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để chuẩn bị dữ liệu import danh sách SIM
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetSimImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Sim_Import_Template.xlsx");
    }
}

