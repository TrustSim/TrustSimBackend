using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

public class FormatRhythmsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tất cả các định dạng nhịp số (Format Rhythm)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<FormatRhythmDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetFormatRhythmsQuery());
        return Ok(ApiResponse<IReadOnlyList<FormatRhythmDto>>.SuccessResult(result, "Lấy danh sách định dạng nhịp số thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một định dạng nhịp số
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<FormatRhythmDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetFormatRhythmByIdQuery(id));
        return Ok(ApiResponse<FormatRhythmDto>.SuccessResult(result, "Lấy thông tin định dạng nhịp số thành công."));
    }

    /// <summary>
    /// [Admin] Thêm mới định dạng nhịp số
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<FormatRhythmDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateFormatRhythmCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<FormatRhythmDto>.SuccessResult(result, "Thêm mới định dạng nhịp số thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật định dạng nhịp số
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<FormatRhythmDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateFormatRhythmCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<FormatRhythmDto>.SuccessResult(result, "Cập nhật định dạng nhịp số thành công."));
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) định dạng nhịp số
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteFormatRhythmCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa định dạng nhịp số thành công."));
    }

    /// <summary>
    /// [Admin] Bulk Import danh sách định dạng nhịp số từ file Excel (.xlsx) / CSV (.csv)
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<FormatRhythmDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromForm] ImportFormatRhythmsRequest request)
    {
        var result = await Mediator.Send(new ImportFormatRhythmsCommand(request.File));
        return Ok(ApiResponse<ImportResult<FormatRhythmDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} thành công."));
    }

    /// <summary>
    /// Xuất danh sách định dạng nhịp số ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export()
    {
        var bytes = await Mediator.Send(new ExportFormatRhythmsQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"FormatRhythms_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để import định dạng nhịp số
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetFormatRhythmImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FormatRhythm_Import_Template.xlsx");
    }
}
