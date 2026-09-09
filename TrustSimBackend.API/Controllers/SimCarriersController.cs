using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Requests;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

public class SimCarriersController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tất cả các nhà mạng viễn thông
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<SimCarrierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var result = await Mediator.Send(new GetCarriersQuery());
        return Ok(ApiResponse<IReadOnlyList<SimCarrierDto>>.SuccessResult(result, "Lấy danh sách nhà mạng thành công."));
    }

    /// <summary>
    /// Lấy thông tin chi tiết nhà mạng kèm các đầu số
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetCarrierByIdQuery(id));
        return Ok(ApiResponse<SimCarrierDto>.SuccessResult(result, "Lấy thông tin chi tiết nhà mạng thành công."));
    }

    /// <summary>
    /// [Admin] Thêm mới nhà mạng viễn thông
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateCarrierCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimCarrierDto>.SuccessResult(result, "Thêm mới nhà mạng thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật thông tin nhà mạng
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<SimCarrierDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateCarrierCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<SimCarrierDto>.SuccessResult(result, "Cập nhật nhà mạng thành công."));
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) nhà mạng
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteCarrierCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa nhà mạng thành công."));
    }

    /// <summary>
    /// [Admin] Bulk Import danh sách nhà mạng viễn thông từ file Excel (.xlsx) / CSV (.csv)
    /// </summary>
    [HttpPost("import")]
    [Authorize(Roles = "ADMIN")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<ImportResult<SimCarrierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import([FromForm] ImportCarriersRequest request)
    {
        var result = await Mediator.Send(new ImportCarriersCommand(request.File));
        return Ok(ApiResponse<ImportResult<SimCarrierDto>>.SuccessResult(result, $"Import hoàn tất: {result.SuccessCount}/{result.TotalRows} thành công."));
    }

    /// <summary>
    /// Xuất danh sách nhà mạng ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export()
    {
        var bytes = await Mediator.Send(new ExportCarriersQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Carriers_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    /// <summary>
    /// Tải file Excel mẫu (.xlsx) để import nhà mạng
    /// </summary>
    [HttpGet("import-template")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImportTemplate()
    {
        var bytes = await Mediator.Send(new GetCarrierImportTemplateQuery());
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Carrier_Import_Template.xlsx");
    }
}

