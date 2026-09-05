using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.System.Commands;
using TrustSimBackend.Application.Features.System.DTOs;
using TrustSimBackend.Application.Features.System.Queries;

namespace TrustSimBackend.API.Controllers;

public class DocumentsController : BaseApiController
{
    /// <summary>
    /// Lấy danh sách tài liệu / quy chế sàn / chính sách
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        var result = await Mediator.Send(new GetDocumentsQuery(activeOnly));
        return Ok(result);
    }

    /// <summary>
    /// Lấy chi tiết tài liệu theo ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetDocumentByIdQuery(id));
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Tạo văn bản điều khoản / chính sách mới
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDocumentRequest request)
    {
        var command = new CreateDocumentCommand(request.DocumentName, request.LinkUrl);
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// [Admin] Cập nhật văn bản điều khoản / chính sách
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateDocumentRequest request)
    {
        var command = new UpdateDocumentCommand(id, request.DocumentName, request.LinkUrl);
        var result = await Mediator.Send(command);
        return Ok(result);
    }
}
