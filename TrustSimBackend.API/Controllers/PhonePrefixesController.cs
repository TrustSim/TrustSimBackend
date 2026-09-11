using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

public class PhonePrefixesController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách các đầu số (có thể lọc theo nhà mạng)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PhonePrefixDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] Guid? carrierId)
    {
        var result = await Mediator.Send(new GetPrefixesQuery(carrierId));
        return Ok(ApiResponse<IReadOnlyList<PhonePrefixDto>>.SuccessResult(result, "Lấy danh sách đầu số thành công."));
    }

    /// <summary>
    /// Lấy chi tiết đầu số theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<PhonePrefixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetPrefixByIdQuery(id));
        return Ok(ApiResponse<PhonePrefixDto>.SuccessResult(result, "Lấy thông tin chi tiết đầu số thành công."));
    }

    /// <summary>
    /// [Admin] Thêm mới đầu số nhà mạng (098, 090, 088...)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PhonePrefixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreatePrefixCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<PhonePrefixDto>.SuccessResult(result, "Thêm mới đầu số thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật đầu số nhà mạng
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PhonePrefixDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdatePrefixCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<PhonePrefixDto>.SuccessResult(result, "Cập nhật đầu số thành công."));
    }

    /// <summary>
    /// [Admin] Xóa đầu số nhà mạng
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeletePrefixCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa đầu số thành công."));
    }

    /// <summary>
    /// [Admin] Bulk Import danh sách đầu số từ file Excel (.xlsx) / CSV (.csv)
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<PhonePrefixDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromForm] ImportPhonePrefixesRequest request)
    {
        var result = await Mediator.Send(new ImportPhonePrefixesCommand(request.File));
        return Ok(ApiResponse<ImportResult<PhonePrefixDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} thành công."));
    }

    /// <summary>
    /// Xuất danh sách đầu số ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] Guid? carrierId)
    {
        var bytes = await Mediator.Send(new ExportPhonePrefixesQuery(carrierId));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PhonePrefixes_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để import đầu số
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetPhonePrefixImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PhonePrefix_Import_Template.xlsx");
    }
}

