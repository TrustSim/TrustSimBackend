using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSimBackend.API.Controllers.Common;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/consultant")]
public class AiConsultantController : BaseApiController
{
    private readonly INvidiaLlmService _nvidiaLlmService;
    private readonly IUnitOfWork _unitOfWork;

    public AiConsultantController(
        INvidiaLlmService nvidiaLlmService,
        IUnitOfWork unitOfWork)
    {
        _nvidiaLlmService = nvidiaLlmService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gửi tin nhắn tư vấn và nhận phản hồi đồng bộ (toàn bộ nội dung cùng gợi ý SIM)
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
    /// Gửi tin nhắn tư vấn và nhận phản hồi streaming thời gian thực (Server-Sent Events) phong cách ChatGPT
    /// </summary>
    [HttpPost("chat/stream")]
    public async Task ChatStream([FromBody] ChatConsultationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Nội dung tin nhắn không được để trống.", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        AiConsultationSession? session = null;
        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            session = await _unitOfWork.AiConsultationSessions.GetByIdAsync(request.SessionId.Value, includeMessages: true, cancellationToken);
        }

        if (session == null)
        {
            var title = request.Message.Length > 50 ? request.Message.Substring(0, 50) + "..." : request.Message;
            session = new AiConsultationSession
            {
                UserAccountId = CurrentUserId != Guid.Empty ? CurrentUserId : null,
                SessionTitle = title,
                TargetBudgetMin = request.BudgetMin,
                TargetBudgetMax = request.BudgetMax,
                UserBirthYear = request.UserBirthYear,
                UserCareer = request.UserCareer,
                FengShuiElement = request.FengShuiElement
            };

            await _unitOfWork.AiConsultationSessions.AddSessionAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Lưu tin nhắn của User
        var userMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.USER,
            MessageContent = request.Message.Trim()
        };
        await _unitOfWork.AiConsultationSessions.AddMessageAsync(userMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Lấy lịch sử hội thoại gần nhất
        var recentMessages = await _unitOfWork.AiConsultationSessions.GetRecentMessagesAsync(session.Id, count: 6, cancellationToken);
        var historyBuilder = new StringBuilder();
        foreach (var msg in recentMessages)
        {
            historyBuilder.AppendLine($"[{(msg.SenderRole == ChatSenderRole.USER ? "Khách hàng" : "Chuyên gia AI")}]: {msg.MessageContent}");
        }

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // Gửi event khởi tạo session info cho client
        var sessionInitJson = JsonSerializer.Serialize(new { sessionId = session.Id, userMessageId = userMsg.Id }, jsonOptions);
        await Response.WriteAsync($"event: session\ndata: {sessionInitJson}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        var fullReplyBuilder = new StringBuilder();
        var suggestedSims = new List<ConsultationSimSuggestion>();
        var referencedChunks = new List<Guid>();

        try
        {
            await foreach (var streamEvent in _nvidiaLlmService.StreamConsultationWithAgenticToolsAsync(
                request.Message,
                historyBuilder.ToString(),
                session.UserBirthYear,
                session.UserCareer,
                session.FengShuiElement?.ToString(),
                session.TargetBudgetMin,
                session.TargetBudgetMax,
                cancellationToken))
            {
                if (streamEvent.EventType == "token" && !string.IsNullOrEmpty(streamEvent.Delta))
                {
                    fullReplyBuilder.Append(streamEvent.Delta);
                }
                if (streamEvent.SuggestedSims is { Count: > 0 })
                {
                    suggestedSims = streamEvent.SuggestedSims;
                }
                if (streamEvent.ReferencedChunkIds is { Count: > 0 })
                {
                    referencedChunks = streamEvent.ReferencedChunkIds;
                }

                var eventJson = JsonSerializer.Serialize(streamEvent, jsonOptions);
                await Response.WriteAsync($"event: {streamEvent.EventType}\ndata: {eventJson}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            var errJson = JsonSerializer.Serialize(new ChatConsultationStreamEvent
            {
                EventType = "error",
                ErrorMessage = "Lỗi kết nối AI: " + ex.Message
            }, jsonOptions);
            await Response.WriteAsync($"event: error\ndata: {errJson}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        // Lưu assistant message vào DB
        var finalContent = fullReplyBuilder.ToString();
        if (!string.IsNullOrWhiteSpace(finalContent))
        {
            var assistantMsg = new AiConsultationMessage
            {
                SessionId = session.Id,
                SenderRole = ChatSenderRole.ASSISTANT,
                MessageContent = finalContent,
                RecommendedSimIds = suggestedSims.Count > 0 ? JsonSerializer.Serialize(suggestedSims) : null,
                ReferencedChunkIds = referencedChunks.Count > 0 ? JsonSerializer.Serialize(referencedChunks) : null,
                TokensUsed = (int)(finalContent.Length / 3.5)
            };
            await _unitOfWork.AiConsultationSessions.AddMessageAsync(assistantMsg, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
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
