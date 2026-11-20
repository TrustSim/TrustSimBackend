using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands;

public class SendConsultationMessageCommandHandler : IRequestHandler<SendConsultationMessageCommand, ChatConsultationResponseDto>
{
    private readonly ISemanticKernelAgentService _agentService;
    private readonly IAiAuthorizationService _aiAuthService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendConsultationMessageCommandHandler> _logger;

    public SendConsultationMessageCommandHandler(
        ISemanticKernelAgentService agentService,
        IAiAuthorizationService aiAuthService,
        IUnitOfWork unitOfWork,
        ILogger<SendConsultationMessageCommandHandler> logger)
    {
        _agentService = agentService;
        _aiAuthService = aiAuthService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ChatConsultationResponseDto> Handle(SendConsultationMessageCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;
        if (string.IsNullOrWhiteSpace(req.Message))
        {
            throw new BadRequestException("Nội dung tin nhắn không được để trống.");
        }

        AiConsultationSession? session = null;

        if (req.SessionId.HasValue && req.SessionId.Value != Guid.Empty)
        {
            session = await _unitOfWork.AiConsultationSessions.GetByIdAsync(req.SessionId.Value, includeMessages: true, cancellationToken);
            if (session != null)
            {
                _aiAuthService.EnsureCanAccessSession(session, command.CurrentUserId, command.CurrentUserRole);
            }
        }

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
            var title = req.Message.Length > 50 ? req.Message.Substring(0, 50) + "..." : req.Message;
            session = new AiConsultationSession
            {
                UserAccountId = command.CurrentUserId,
                SessionTitle = title,
                TargetBudgetMin = req.BudgetMin,
                TargetBudgetMax = req.BudgetMax,
                UserBirthYear = req.UserBirthYear,
                UserCareer = req.UserCareer,
                FengShuiElement = req.FengShuiElement
            };

            await _unitOfWork.AiConsultationSessions.AddSessionAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var userMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.USER,
            MessageContent = req.Message.Trim()
        };
        await _unitOfWork.AiConsultationSessions.AddMessageAsync(userMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var userContext = new AiUserContext
        {
            UserId = command.CurrentUserId,
            Role = command.CurrentUserRole,
            ClientIp = command.ClientIp
        };

        var agentResponse = await _agentService.GenerateConsultationWithAgenticToolsAsync(
            req.Message,
            historyBuilder.ToString(),
            session.UserBirthYear,
            session.UserCareer,
            session.FengShuiElement?.ToString(),
            session.TargetBudgetMin,
            session.TargetBudgetMax,
            cancellationToken,
            userContext,
            session.Id);

        var recommendedSimsJson = agentResponse.SuggestedSims.Count > 0
            ? JsonSerializer.Serialize(agentResponse.SuggestedSims)
            : null;

        var referencedChunksJson = agentResponse.ReferencedChunkIds.Count > 0
            ? JsonSerializer.Serialize(agentResponse.ReferencedChunkIds)
            : null;

        var tokensUsed = agentResponse.TokensUsed > 0
            ? agentResponse.TokensUsed
            : (int)(agentResponse.ReplyContent.Length / 3.5);

        var assistantMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.ASSISTANT,
            MessageContent = agentResponse.ReplyContent,
            RecommendedSimIds = recommendedSimsJson,
            ReferencedChunkIds = referencedChunksJson,
            TokensUsed = tokensUsed
        };

        await _unitOfWork.AiConsultationSessions.AddMessageAsync(assistantMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatConsultationResponseDto
        {
            SessionId = session.Id,
            MessageId = assistantMsg.Id,
            ReplyContent = agentResponse.ReplyContent,
            SuggestedSims = agentResponse.SuggestedSims.Select(s => new ConsultationSimDto
            {
                SimNumberId = s.SimNumberId,
                PhoneNumber = s.PhoneNumber,
                Price = s.Price,
                CarrierName = s.CarrierName,
                HighlightReason = s.HighlightReason
            }).ToList(),
            ReferencedDocuments = agentResponse.ReferencedDocuments,
            ReferencedChunkIds = agentResponse.ReferencedChunkIds,
            EngineSource = agentResponse.EngineSource,
            ModelUsed = agentResponse.ModelUsed,
            Intent = agentResponse.Intent,
            CacheHit = agentResponse.CacheHit,
            GuardrailBlocked = agentResponse.GuardrailBlocked,
            TokensUsed = tokensUsed,
            ExecutionTimeMs = agentResponse.ExecutionTimeMs
        };
    }
}
