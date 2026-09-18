using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Application.Features.Sim.Queries;

namespace TrustSimBackend.API.Controllers;

/// <summary>
/// Quản lý tài nguyên Danh mục phân loại SIM (Tứ quý, Thần tài, Lộc phát...).
/// </summary>
public class CategoriesController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tất cả các thể loại phân loại SIM
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SimCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetCategoriesQuery());
        return Ok(ApiResponse<IReadOnlyList<SimCategoryDto>>.SuccessResult(result, "Lấy danh sách thể loại SIM thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một thể loại SIM
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SimCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetCategoryByIdQuery(id));
        return Ok(ApiResponse<SimCategoryDto>.SuccessResult(result, "Lấy thông tin thể loại SIM thành công."));
    }

    /// <summary>
    /// [Admin] Thêm mới thể loại SIM (Thần tài, Tứ quý...)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimCategoryDto>.SuccessResult(result, "Thêm mới thể loại SIM thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật thể loại SIM
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCategoryCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<SimCategoryDto>.SuccessResult(result, "Cập nhật thể loại SIM thành công."));
    }

    /// <summary>
    /// [Admin] Xóa thể loại SIM
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteCategoryCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa thể loại SIM thành công."));
    }

    /// <summary>
    /// [Admin] Bulk Import danh mục thể loại SIM từ file Excel (.xlsx) / CSV (.csv)
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<SimCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromForm] ImportCategoriesRequest request)
    {
        var result = await Mediator.Send(new ImportCategoriesCommand(request.File));
        return Ok(ApiResponse<ImportResult<SimCategoryDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} thành công."));
    }

    /// <summary>
    /// Xuất danh sách thể loại SIM ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export()
    {
        var bytes = await Mediator.Send(new ExportCategoriesQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SimCategories_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để import thể loại SIM
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetCategoryImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SimCategory_Import_Template.xlsx");
    }
}
