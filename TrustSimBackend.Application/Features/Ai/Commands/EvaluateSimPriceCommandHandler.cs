using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Application.Features.Ai.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands;

public class EvaluateSimPriceCommandHandler : IRequestHandler<EvaluateSimPriceCommand, SimValuationResponseDto>
{
    private readonly ISimFeatureExtractor _featureExtractor;
    private readonly ISimValuationMathEngine _mathEngine;
    private readonly IRagRetrievalService _ragService;
    private readonly ISemanticKernelAgentService _agentService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EvaluateSimPriceCommandHandler> _logger;

    public EvaluateSimPriceCommandHandler(
        ISimFeatureExtractor featureExtractor,
        ISimValuationMathEngine mathEngine,
        IRagRetrievalService ragService,
        ISemanticKernelAgentService agentService,
        IUnitOfWork unitOfWork,
        ILogger<EvaluateSimPriceCommandHandler> logger)
    {
        _featureExtractor = featureExtractor;
        _mathEngine = mathEngine;
        _ragService = ragService;
        _agentService = agentService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<SimValuationResponseDto> Handle(EvaluateSimPriceCommand request, CancellationToken cancellationToken)
    {
        SimNumber? simNumber = null;
        string targetPhoneNumber = string.Empty;
        string? carrierName = null;
        float prefixScore = 1.0f;

        if (request.SimNumberId.HasValue && request.SimNumberId.Value != Guid.Empty)
        {
            simNumber = await _unitOfWork.SimNumbers.GetByIdAsync(request.SimNumberId.Value, cancellationToken);
            if (simNumber == null)
            {
                throw new NotFoundException($"Không tìm thấy số SIM với ID {request.SimNumberId.Value}");
            }

            targetPhoneNumber = simNumber.PhoneNumber;
            carrierName = simNumber.PhonePrefix?.SimCarrier?.CarrierName;
            prefixScore = simNumber.PhonePrefix?.PrefixReputationScore ?? 1.0f;
        }
        else if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
        {
            targetPhoneNumber = request.PhoneNumber.Trim();
        }
        else
        {
            throw new BadRequestException("Vui lòng cung cấp simNumberId hoặc chuỗi số điện thoại phoneNumber để định giá.");
        }

        _logger.LogInformation("Bắt đầu quy trình định giá Hybrid AI 4 tầng cho số SIM {Phone}", targetPhoneNumber);

        // ─── TẦNG 1: Khảo sát đa nhịp số & Bóc tách tổ hợp tối ưu ───
        var features = _featureExtractor.ExtractFeatures(targetPhoneNumber, carrierName, prefixScore);

        // ─── TẦNG 2: Tính giá sàn cơ sở toán học theo thế nhịp tối ưu ───
        var mathResult = _mathEngine.CalculateBaselinePrice(features);

        // ─── TẦNG 3: RAG Retrieval trích xuất báo cáo thị trường & phong thủy ───
        var ragQuery = $"Thị trường định giá SIM số đẹp {features.CarrierName} đầu số {features.Prefix} thế số {features.DetectedRhythmName} {features.SelectedRhythmValue} lộc phát thần tài";
        var ragChunks = await _ragService.RetrieveRelevantChunksAsync(ragQuery, topK: 3, minSimilarity: 0.35, cancellationToken);

        // ─── TẦNG 4: Tổng hợp, So sánh thế số & Giải trình bằng Google Gemini qua Semantic Kernel ───
        var llmResult = await _agentService.GenerateValuationReasoningAsync(features, mathResult, ragChunks, cancellationToken);

        // Lưu lại bản ghi SimValuation vào cơ sở dữ liệu nếu có simNumber
        var referencedChunkIdsJson = ragChunks.Count > 0
            ? JsonSerializer.Serialize(ragChunks.Select(c => c.ChunkId).ToList())
            : null;

        var valuation = new SimValuation
        {
            SimNumberId = simNumber?.Id ?? Guid.Empty,
            UserAccountId = request.CurrentUserId,
            SuggestedPrice = llmResult.SuggestedPrice > 0 ? llmResult.SuggestedPrice : mathResult.BaselinePrice,
            MinPrice = llmResult.MinPrice > 0 ? llmResult.MinPrice : Math.Round(mathResult.BaselinePrice * 0.85m),
            MaxPrice = llmResult.MaxPrice > 0 ? llmResult.MaxPrice : Math.Round(mathResult.BaselinePrice * 1.2m),
            ConfidenceScore = llmResult.ConfidenceScore,
            BaselinePrice = mathResult.BaselinePrice,
            MarketMultiplier = llmResult.MarketMultiplier,
            ValuationReasoning = llmResult.ValuationReasoning,
            FengShuiAnalysis = llmResult.FengShuiSummary,
            ReferencedChunkIds = referencedChunkIdsJson,
            AiModelName = string.IsNullOrEmpty(llmResult.EngineSource) ? "gemini-3.1-flash-lite" : llmResult.EngineSource,
            AppliedToSim = false
        };

        if (simNumber != null)
        {
            await _unitOfWork.SimValuations.AddAsync(valuation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new SimValuationResponseDto
        {
            ValuationId = valuation.Id,
            SimNumberId = simNumber?.Id,
            PhoneNumber = features.CleanPhoneNumber,
            FormattedNumber = string.IsNullOrEmpty(llmResult.FormattedNumber) ? features.FormattedNumber : llmResult.FormattedNumber,
            SuggestedPrice = valuation.SuggestedPrice,
            MinPrice = valuation.MinPrice,
            MaxPrice = valuation.MaxPrice,
            TotalPoints = llmResult.TotalPoints > 0 ? llmResult.TotalPoints : features.TotalPoints,
            PrefixGroup = features.PrefixGroup,
            MiddleGroup = features.MiddleGroup,
            TailGroup = features.TailGroup,
            ConfidenceScore = valuation.ConfidenceScore,
            BaselinePrice = valuation.BaselinePrice,
            MarketMultiplier = valuation.MarketMultiplier,
            SelectedRhythmValue = features.SelectedRhythmValue,
            DetectedRhythmName = features.DetectedRhythmName,
            DetectedRhythmCode = features.DetectedRhythmCode,
            DetectedCategories = features.DetectedCategories.Select(c => $"{c.CategoryName} ({c.CategoryCode})").ToList(),
            ValuationReasoning = valuation.ValuationReasoning,
            FengShuiAnalysis = valuation.FengShuiAnalysis,
            AlternativeRhythmsComparison = features.AlternativeRhythmsComparison,
            ReferencedDocuments = ragChunks.Select(c => c.DocumentName).Distinct().ToList(),
            AiModelName = string.IsNullOrEmpty(llmResult.EngineSource) ? "gemini-3.1-flash-lite" : llmResult.EngineSource,
            EngineSource = llmResult.EngineSource,
            ExecutionTimeMs = llmResult.ExecutionTimeMs,
            AppliedToSim = valuation.AppliedToSim,
            CreatedDate = valuation.CreatedDate
        };
    }
}
