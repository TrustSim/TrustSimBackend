using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands.SendConsultationMessage;

public record SendConsultationMessageCommand(
    ChatConsultationRequest Request,
    Guid? CurrentUserId
) : IRequest<ChatConsultationResponseDto>;

public class SendConsultationMessageCommandHandler : IRequestHandler<SendConsultationMessageCommand, ChatConsultationResponseDto>
{
    private readonly IAiConsultationSessionRepository _sessionRepository;
    private readonly ISimNumberRepository _simNumberRepository;
    private readonly IRagRetrievalService _ragService;
    private readonly INvidiaLlmService _nvidiaLlmService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendConsultationMessageCommandHandler> _logger;

    public SendConsultationMessageCommandHandler(
        IAiConsultationSessionRepository sessionRepository,
        ISimNumberRepository simNumberRepository,
        IRagRetrievalService ragService,
        INvidiaLlmService nvidiaLlmService,
        IUnitOfWork unitOfWork,
        ILogger<SendConsultationMessageCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _simNumberRepository = simNumberRepository;
        _ragService = ragService;
        _nvidiaLlmService = nvidiaLlmService;
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
            session = await _sessionRepository.GetByIdAsync(req.SessionId.Value, includeMessages: true, cancellationToken);
        }

        if (session == null)
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

            await _sessionRepository.AddSessionAsync(session, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        // Lưu tin nhắn của User
        var userMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.USER,
            MessageContent = req.Message.Trim()
        };
        await _sessionRepository.AddMessageAsync(userMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Lấy lịch sử hội thoại gần nhất
        var recentMessages = await _sessionRepository.GetRecentMessagesAsync(session.Id, count: 6, cancellationToken);
        var historyBuilder = new StringBuilder();
        foreach (var msg in recentMessages)
        {
            historyBuilder.AppendLine($"[{(msg.SenderRole == ChatSenderRole.USER ? "Khách hàng" : "Chuyên gia AI")}]: {msg.MessageContent}");
        }

        // Trích xuất tri thức phong thủy qua RAG
        var ragQuery = $"Quy tắc phong thủy ý nghĩa số SIM {req.Message} năm sinh {session.UserBirthYear} ngành nghề {session.UserCareer} mệnh {session.FengShuiElement}";
        var ragChunks = await _ragService.RetrieveRelevantChunksAsync(ragQuery, topK: 3, minSimilarity: 0.35, cancellationToken);

        // Truy vấn kho SIM thực tế có sẵn (AVAILABLE hoặc LISTED) trên sàn TrustSim
        var matchingSims = await _simNumberRepository.GetAvailableSimsForConsultationAsync(
            session.TargetBudgetMin,
            session.TargetBudgetMax,
            limit: 6,
            cancellationToken);

        var simSuggestions = matchingSims.Select(s => new ConsultationSimSuggestion
        {
            SimNumberId = s.Id,
            PhoneNumber = s.PhoneNumber,
            Price = s.Price,
            CarrierName = s.PhonePrefix?.SimCarrier?.CarrierName ?? "Viễn thông",
            HighlightReason = $"{s.FormatRhythm?.FormatRhythmName ?? "Số đẹp"} - Đầu {s.PhonePrefix?.PrefixValue}"
        }).ToList();

        // Gọi NVIDIA LLM sinh phản hồi tư vấn
        var aiReplyText = await _nvidiaLlmService.GenerateConsultationResponseAsync(
            req.Message,
            historyBuilder.ToString(),
            ragChunks,
            simSuggestions,
            cancellationToken);

        var recommendedSimsJson = simSuggestions.Count > 0
            ? JsonSerializer.Serialize(simSuggestions)
            : null;

        var referencedChunksJson = ragChunks.Count > 0
            ? JsonSerializer.Serialize(ragChunks.Select(c => c.ChunkId).ToList())
            : null;

        // Lưu tin nhắn phản hồi của Assistant
        var assistantMsg = new AiConsultationMessage
        {
            SessionId = session.Id,
            SenderRole = ChatSenderRole.ASSISTANT,
            MessageContent = aiReplyText,
            RecommendedSimIds = recommendedSimsJson,
            ReferencedChunkIds = referencedChunksJson,
            TokensUsed = (int)(aiReplyText.Length / 3.5)
        };

        await _sessionRepository.AddMessageAsync(assistantMsg, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatConsultationResponseDto
        {
            SessionId = session.Id,
            MessageId = assistantMsg.Id,
            ReplyContent = aiReplyText,
            SuggestedSims = simSuggestions.Select(s => new ConsultationSimDto
            {
                SimNumberId = s.SimNumberId,
                PhoneNumber = s.PhoneNumber,
                Price = s.Price,
                CarrierName = s.CarrierName,
                HighlightReason = s.HighlightReason
            }).ToList(),
            ReferencedDocuments = ragChunks.Select(c => c.DocumentName).Distinct().ToList()
        };
    }
}
