using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/valuation")]
public class AiValuationController : BaseApiController
{
    /// <summary>
    /// Thực hiện thẩm định &amp; định giá SIM bằng Mô hình Hybrid 4 Tầng (Toán học + RAG + NVIDIA LLM)
    /// </summary>
    [HttpPost("estimate")]
    [ProducesResponseType(typeof(ApiResponse<SimValuationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EstimatePrice([FromBody] EvaluateSimPriceRequest request)
    {
        var command = new EvaluateSimPriceCommand(request.SimNumberId, request.PhoneNumber, CurrentUserId != Guid.Empty ? CurrentUserId : null);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<SimValuationResponseDto>.SuccessResult(result, "Định giá SIM bằng trí tuệ nhân tạo thành công."));
    }

    /// <summary>
    /// Áp dụng mức giá do AI định giá trực tiếp vào số SIM
    /// </summary>
    [HttpPost("apply/{valuationId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ApplyValuationPrice([FromRoute] Guid valuationId)
    {
        var command = new ApplyValuationPriceCommand(valuationId, CurrentUserId);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResult(result, "Đã áp dụng mức giá định giá vào số SIM thành công."));
    }

    /// <summary>
    /// Lấy lịch sử tất cả các lần định giá của một số SIM (lọc theo model AI, trạng thái áp dụng, phân trang)
    /// </summary>
    [HttpGet("sim/{simNumberId:guid}/history")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SimValuationResponseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetValuationHistory([FromRoute] Guid simNumberId, [FromQuery] SimValuationQueryFilter filter)
    {
        var result = await Mediator.Send(new GetSimValuationHistoryQuery(simNumberId, filter));
        return HandlePagedResult(result, "Lấy lịch sử định giá SIM thành công.");
    }
}
