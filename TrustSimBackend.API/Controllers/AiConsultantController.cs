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
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.API.Controllers;

[Route("api/ai/consultant")]
public class AiConsultantController : BaseApiController
{
    private readonly ISemanticKernelAgentService _agentService;
    private readonly IAiAuthorizationService _aiAuthService;
    private readonly IMcpToolServer _mcpServer;
    private readonly IAiTelemetryAndEvalService _telemetryService;
    private readonly IUnitOfWork _unitOfWork;

    public AiConsultantController(
        ISemanticKernelAgentService agentService,
        IAiAuthorizationService aiAuthService,
        IMcpToolServer mcpServer,
        IAiTelemetryAndEvalService telemetryService,
        IUnitOfWork unitOfWork)
    {
        _agentService = agentService;
        _aiAuthService = aiAuthService;
        _mcpServer = mcpServer;
        _telemetryService = telemetryService;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Gửi tin nhắn tư vấn và nhận phản hồi đồng bộ (toàn bộ nội dung cùng gợi ý SIM)
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ApiResponse<ChatConsultationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Chat([FromBody] ChatConsultationRequest request)
    {
        var userId = CurrentUserId != Guid.Empty ? CurrentUserId : (Guid?)null;
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var command = new SendConsultationMessageCommand(request, userId, CurrentUser.Role, clientIp);
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

        if (request.Message.Trim().Length > 2000)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Nội dung tin nhắn tối đa 2000 ký tự.", cancellationToken);
            return;
        }

        var currentUserId = CurrentUserId != Guid.Empty ? CurrentUserId : (Guid?)null;

        AiConsultationSession? session = null;
        if (request.SessionId.HasValue && request.SessionId.Value != Guid.Empty)
        {
            session = await _unitOfWork.AiConsultationSessions.GetByIdAsync(request.SessionId.Value, includeMessages: true, cancellationToken);
            if (session != null)
            {
                _aiAuthService.EnsureCanAccessSession(session, currentUserId, CurrentUser.Role);
            }
        }

        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        bool isNewSession = session == null;
        var historyBuilder = new StringBuilder();

        if (!isNewSession)
        {
            // Load prior messages BEFORE saving the new user message so Turn 1 preserves empty history for response cache eligibility.
            var recentMessages = await _unitOfWork.AiConsultationSessions.GetRecentMessagesAsync(session!.Id, count: 6, cancellationToken);
            foreach (var msg in recentMessages)
            {
                historyBuilder.AppendLine($"[{(msg.SenderRole == ChatSenderRole.USER ? "Khách hàng" : "Chuyên gia AI")}]: {msg.MessageContent}");
            }
        }
        else
        {
            var title = request.Message.Length > 50 ? request.Message.Substring(0, 50) + "..." : request.Message;
            session = new AiConsultationSession
            {
                UserAccountId = currentUserId,
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

        var userMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.USER,
            MessageContent = request.Message.Trim()
        };
        await _unitOfWork.AiConsultationSessions.AddMessageAsync(userMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        var sessionInitJson = JsonSerializer.Serialize(new { sessionId = session.Id, userMessageId = userMsg.Id }, jsonOptions);
        await Response.WriteAsync($"event: session\ndata: {sessionInitJson}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);

        var fullReplyBuilder = new StringBuilder();
        var suggestedSims = new List<ConsultationSimSuggestion>();
        var referencedChunks = new List<Guid>();
        var streamedTokensUsed = 0;

        var userContext = BuildAiUserContext();

        try
        {
            await foreach (var streamEvent in _agentService.StreamConsultationWithAgenticToolsAsync(
                request.Message,
                historyBuilder.ToString(),
                session.UserBirthYear,
                session.UserCareer,
                session.FengShuiElement?.ToString(),
                session.TargetBudgetMin,
                session.TargetBudgetMax,
                cancellationToken,
                userContext,
                session.Id))
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
                if (streamEvent.TokensUsed is > 0)
                {
                    streamedTokensUsed = streamEvent.TokensUsed.Value;
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
                TokensUsed = streamedTokensUsed > 0 ? streamedTokensUsed : (int)(finalContent.Length / 3.5)
            };
            await _unitOfWork.AiConsultationSessions.AddMessageAsync(assistantMsg, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Lấy danh sách các phiên tư vấn của người dùng hiện tại (tìm kiếm, sắp xếp, phân trang)
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ConsultationSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions([FromQuery] ConsultationSessionQueryFilter filter)
    {
        var result = await Mediator.Send(new GetConsultationSessionsQuery(CurrentUserId, filter));
        return HandlePagedResult(result, "Lấy danh sách phiên tư vấn thành công.");
    }

    /// <summary>
    /// Lấy danh sách tin nhắn trong một phiên tư vấn (bảo vệ cách ly phiên đăng nhập của từng người dùng)
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ConsultationMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMessages([FromRoute] Guid sessionId, [FromQuery] ConsultationMessageQueryFilter filter)
    {
        var userId = CurrentUserId != Guid.Empty ? CurrentUserId : (Guid?)null;
        var result = await Mediator.Send(new GetConsultationMessagesQuery(sessionId, filter, userId, CurrentUser.Role));
        return HandlePagedResult(result, "Lấy danh sách tin nhắn thành công.");
    }

    /// <summary>
    /// Liệt kê danh sách công cụ MCP được cấp phép theo quyền người dùng hiện tại
    /// </summary>
    [HttpGet("mcp/tools")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<McpToolDescriptor>>), StatusCodes.Status200OK)]
    public IActionResult ListAuthorizedMcpTools()
    {
        var tools = _mcpServer.ListAuthorizedTools(BuildAiUserContext());
        return Ok(ApiResponse<IReadOnlyList<McpToolDescriptor>>.SuccessResult(tools, "Lấy danh sách công cụ MCP thành công."));
    }

    /// <summary>
    /// Giao thức MCP JSON-RPC 2.0 (server/discover, tools/list, tools/call) có kiểm soát phân quyền và rào chắn
    /// </summary>
    [HttpPost("mcp")]
    [ProducesResponseType(typeof(McpJsonRpcResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> InvokeMcpJsonRpc([FromBody] McpJsonRpcRequest request, CancellationToken cancellationToken)
    {
        var response = await _mcpServer.HandleJsonRpcAsync(request, BuildAiUserContext(), cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// [ADMIN] Báo cáo giám sát vận hành AI (Latency, Tokens, Cache Hit Rate, Guardrails, Eval Score) từ PostgreSQL
    /// </summary>
    [HttpGet("telemetry")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AiTelemetryDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTelemetryDashboard([FromQuery] int lastHours = 24, CancellationToken cancellationToken = default)
    {
        var summary = await _telemetryService.GetDashboardSummaryAsync(lastHours, cancellationToken);
        return Ok(ApiResponse<AiTelemetryDashboardDto>.SuccessResult(summary, "Lấy báo cáo giám sát AI Telemetry thành công."));
    }

    /// <summary>
    /// [ADMIN] Lấy lịch sử kết quả chấm điểm tự động (Online Auto-Eval &amp; Benchmark Suite)
    /// </summary>
    [HttpGet("eval/results")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AiEvaluationItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvaluationResults(
        [FromQuery] string? evalType = null,
        [FromQuery] bool? passed = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _telemetryService.GetEvaluationResultsAsync(evalType, passed, pageIndex, pageSize, cancellationToken);
        return HandlePagedResult(result, "Lấy danh sách kết quả đánh giá AI thành công.");
    }

    /// <summary>
    /// [ADMIN] Chạy bộ kiểm thử đánh giá tự động (Automated Benchmark Eval Suite) qua 8 lớp AI Chatbot
    /// </summary>
    [HttpPost("eval/run")]
    [Authorize(Roles = "ADMIN")]
    [ProducesResponseType(typeof(ApiResponse<AiBenchmarkRunReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RunEvaluationBenchmarkSuite(CancellationToken cancellationToken = default)
    {
        var report = await _telemetryService.RunBenchmarkSuiteAsync(cancellationToken);
        return Ok(ApiResponse<AiBenchmarkRunReportDto>.SuccessResult(report, "Hoàn tất chạy bộ đánh giá tự động AI Chatbot."));
    }

    private AiUserContext BuildAiUserContext()
    {
        return new AiUserContext
        {
            UserId = CurrentUserId != Guid.Empty ? CurrentUserId : null,
            Role = CurrentUser.Role ?? "GUEST",
            ClientIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };
    }
}
