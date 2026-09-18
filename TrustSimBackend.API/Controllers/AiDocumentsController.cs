using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/documents")]
public class AiDocumentsController : BaseApiController
{
    /// <summary>
    /// [Admin] Nạp tài liệu nghiên cứu thị trường / quy tắc định giá từ URL để chạy RAG Ingestion Pipeline
    /// </summary>
    [HttpPost("ingest")]
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
    [HttpPost("upload")]
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
    [HttpGet("{id:guid}/status")]
    [Authorize(Roles = "ADMIN,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus([FromRoute] Guid id)
    {
        if (!IsAdmin)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.FailureResult("Yêu cầu quyền Quản trị viên (ADMIN) để xem trạng thái tài liệu RAG."));
        }

        var result = await Mediator.Send(new GetDocumentIngestStatusQuery(id));
        return Ok(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, "Lấy trạng thái xử lý tài liệu thành công."));
    }
}
