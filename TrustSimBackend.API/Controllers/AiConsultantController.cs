using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.Commands.SendConsultationMessage;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Application.Features.Ai.Queries.GetConsultationMessages;
using TrustSimBackend.Application.Features.Ai.Queries.GetConsultationSessions;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/consultant")]
public class AiConsultantController : BaseApiController
{
    /// <summary>
    /// Gửi tin nhắn tư vấn và nhận phân tích phong thủy &amp; gợi ý danh sách SIM thực tế từ AI
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatConsultationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Chat([FromBody] ChatConsultationRequest request)
    {
        var command = new SendConsultationMessageCommand(request, CurrentUserId != Guid.Empty ? CurrentUserId : null);
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<ChatConsultationResponseDto>.SuccessResult(result, "Nhận phản hồi tư vấn thành công."));
    }

    /// <summary>
    /// Lấy danh sách các phiên tư vấn của người dùng hiện tại
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConsultationSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions()
    {
        var result = await Mediator.Send(new GetConsultationSessionsQuery(CurrentUserId));
        return Ok(ApiResponse<IReadOnlyList<ConsultationSessionDto>>.SuccessResult(result, "Lấy danh sách phiên tư vấn thành công."));
    }

    /// <summary>
    /// Lấy toàn bộ tin nhắn trong một phiên tư vấn
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ConsultationMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages([FromRoute] Guid sessionId)
    {
        var result = await Mediator.Send(new GetConsultationMessagesQuery(sessionId));
        return Ok(ApiResponse<IReadOnlyList<ConsultationMessageDto>>.SuccessResult(result, "Lấy danh sách tin nhắn thành công."));
    }
}
