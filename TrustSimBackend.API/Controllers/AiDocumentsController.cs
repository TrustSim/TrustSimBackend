using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.Commands.IngestDocument;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Application.Features.Ai.Queries.GetDocumentIngestStatus;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/documents")]
public class AiDocumentsController : BaseApiController
{
    /// <summary>
    /// [Admin] Nạp tài liệu nghiên cứu thị trường / quy tắc định giá từ URL để chạy RAG Ingestion Pipeline
    /// </summary>
    [HttpPost("ingest")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> IngestDocument([FromBody] IngestDocumentRequest request)
    {
        var command = new IngestDocumentCommand(request.DocumentName, request.LinkUrl, request.DocumentType);
        var result = await Mediator.Send(command);
        return Accepted(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, "Yêu cầu nạp và đánh chỉ mục tài liệu đã được tiếp nhận và đang xử lý nền."));
    }

    /// <summary>
    /// Kiểm tra tiến độ xử lý và số lượng vector chunks đã tạo của tài liệu
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<IngestDocumentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus([FromRoute] Guid id)
    {
        var result = await Mediator.Send(new GetDocumentIngestStatusQuery(id));
        return Ok(ApiResponse<IngestDocumentResponseDto>.SuccessResult(result, "Lấy trạng thái xử lý tài liệu thành công."));
    }
}
