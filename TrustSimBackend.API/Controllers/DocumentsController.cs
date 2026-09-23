using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Application.Features.System.DTOs.Requests;
using TrustSimBackend.Application.Features.System.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

public class DocumentsController : BaseApiController
{
    // ─────────────────────────────────────────────────────────────────────────────
    // QUY CHẾ, CHÍNH SÁCH & VĂN BẢN SÀN
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách tài liệu / quy chế sàn / chính sách
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool activeOnly = true)
    {
        var result = await Mediator.Send(new GetDocumentsQuery(activeOnly));
        return Ok(ApiResponse<IReadOnlyList<DocumentDto>>.SuccessResult(result, "Lấy danh sách văn bản thành công."));
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
        return Ok(ApiResponse<DocumentDto>.SuccessResult(result, "Lấy chi tiết văn bản thành công."));
    }

    /// <summary>
    /// [Admin] Cập nhật văn bản điều khoản / chính sách
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<DocumentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] UpdateDocumentCommand command)
    {
        var result = await Mediator.Send(command with { Id = id });
        return Ok(ApiResponse<DocumentDto>.SuccessResult(result, "Cập nhật văn bản điều khoản thành công."));
    }

    /// <summary>
    /// [Admin] Xóa (xóa mềm) tài liệu / chính sách
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new DeleteDocumentCommand(id));
        return Ok(ApiResponse<bool>.SuccessResult(result, "Xóa văn bản thành công."));
    }

    /// <summary>
    /// Xuất danh mục tài liệu / chính sách ra file Excel (.xlsx)
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] bool? activeOnly)
    {
        var bytes = await Mediator.Send(new ExportDocumentsQuery(activeOnly));
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Documents_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // NẠP & QUẢN LÝ TÀI LIỆU AI RAG
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// [Admin] Nạp tài liệu nghiên cứu thị trường / quy tắc định giá từ URL để chạy RAG Ingestion Pipeline
    /// </summary>
    [HttpPost("ai/ingest")]
    [HttpPost("~/api/ai/documents/ingest")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> IngestDocument([FromBody] IngestDocumentRequest request)
    {
        if (!IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.FailureResult("Yêu cầu quyền Quản trị viên (ADMIN) để nạp tài liệu RAG."));
        }

        var command = new IngestDocumentCommand(request.DocumentName, request.LinkUrl, request.DocumentType);
        var result = await Mediator.Send(command);
        return Accepted(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, "Yêu cầu nạp và đánh chỉ mục tài liệu đã được tiếp nhận và đang xử lý nền."));
    }

    /// <summary>
    /// [Admin] Tải lên trực tiếp tệp tài liệu (PDF, TXT, MD, DOCX, CSV) để chạy RAG Ingestion Pipeline
    /// </summary>
    [HttpPost("ai/upload")]
    [HttpPost("~/api/ai/documents/upload")]
    [Authorize(Roles = "ADMIN,Admin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadDocument([FromForm] UploadDocumentRequest request)
    {
        if (!IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.FailureResult("Yêu cầu quyền Quản trị viên (ADMIN) để tải lên tài liệu RAG."));
        }

        if (request.File == null || request.File.Length == 0)
        {
            return BadRequest(ApiResponse<object>.FailureResult("Vui lòng chọn tệp tin tài liệu hợp lệ để tải lên."));
        }

        var command = new UploadDocumentCommand(
            request.DocumentName ?? Path.GetFileNameWithoutExtension(request.File.FileName),
            request.File,
            request.DocumentType);

        var result = await Mediator.Send(command);
        return Accepted(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, $"Tệp '{request.File.FileName}' đã được tiếp nhận và đang trích xuất RAG Embeddings trong nền."));
    }

    /// <summary>
    /// Kiểm tra tiến độ xử lý và số lượng vector chunks đã tạo của tài liệu
    /// </summary>
    [HttpGet("ai/{id:guid}/status")]
    [HttpGet("~/api/ai/documents/{id:guid}/status")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAiDocumentStatus([FromRoute] Guid id)
    {
        if (!IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.FailureResult("Yêu cầu quyền Quản trị viên (ADMIN) để xem trạng thái tài liệu RAG."));
        }

        var result = await Mediator.Send(new GetDocumentIngestStatusQuery(id));
        return Ok(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, "Lấy trạng thái xử lý tài liệu thành công."));
    }
}


