using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.DTOs;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands.EvaluateSimPrice;

public record EvaluateSimPriceCommand(
    Guid? SimNumberId,
    string? PhoneNumber,
    Guid? CurrentUserId
) : IRequest<SimValuationResponseDto>;

public class EvaluateSimPriceCommandHandler : IRequestHandler<EvaluateSimPriceCommand, SimValuationResponseDto>
{
    private readonly ISimNumberRepository _simNumberRepository;
    private readonly ISimFeatureExtractor _featureExtractor;
    private readonly ISimValuationMathEngine _mathEngine;
    private readonly IRagRetrievalService _ragService;
    private readonly INvidiaLlmService _nvidiaLlmService;
    private readonly ISimValuationRepository _valuationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<EvaluateSimPriceCommandHandler> _logger;

    public EvaluateSimPriceCommandHandler(
        ISimNumberRepository simNumberRepository,
        ISimFeatureExtractor featureExtractor,
        ISimValuationMathEngine mathEngine,
        IRagRetrievalService ragService,
        INvidiaLlmService nvidiaLlmService,
        ISimValuationRepository valuationRepository,
        IUnitOfWork unitOfWork,
        ILogger<EvaluateSimPriceCommandHandler> logger)
    {
        _simNumberRepository = simNumberRepository;
        _featureExtractor = featureExtractor;
        _mathEngine = mathEngine;
        _ragService = ragService;
        _nvidiaLlmService = nvidiaLlmService;
        _valuationRepository = valuationRepository;
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
            simNumber = await _simNumberRepository.GetByIdAsync(request.SimNumberId.Value, cancellationToken);
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

        // ─── TẦNG 1: Bóc tách đặc trưng số học ───
        var features = _featureExtractor.ExtractFeatures(targetPhoneNumber, carrierName, prefixScore);

        // ─── TẦNG 2: Tính giá sàn cơ sở toán học ───
        var mathResult = _mathEngine.CalculateBaselinePrice(features);

        // ─── TẦNG 3: RAG Retrieval trích xuất báo cáo thị trường ───
        var ragQuery = $"Thị trường định giá SIM số đẹp {features.CarrierName} đầu số {features.Prefix} thế số {features.DetectedRhythmName} lộc phát thần tài";
        var ragChunks = await _ragService.RetrieveRelevantChunksAsync(ragQuery, topK: 3, minSimilarity: 0.35, cancellationToken);

        // ─── TẦNG 4: Tổng hợp & Giải trình bằng NVIDIA LLM ───
        var llmResult = await _nvidiaLlmService.GenerateValuationReasoningAsync(features, mathResult, ragChunks, cancellationToken);

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
            AiModelName = "meta/llama-3.3-70b-instruct",
            AppliedToSim = false
        };

        if (simNumber != null)
        {
            await _valuationRepository.AddAsync(valuation, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new SimValuationResponseDto
        {
            ValuationId = valuation.Id,
            SimNumberId = simNumber?.Id,
            PhoneNumber = features.CleanPhoneNumber,
            SuggestedPrice = valuation.SuggestedPrice,
            MinPrice = valuation.MinPrice,
            MaxPrice = valuation.MaxPrice,
            ConfidenceScore = valuation.ConfidenceScore,
            BaselinePrice = valuation.BaselinePrice,
            MarketMultiplier = valuation.MarketMultiplier,
            DetectedRhythmName = features.DetectedRhythmName,
            DetectedRhythmCode = features.DetectedRhythmCode,
            DetectedCategories = features.DetectedCategories.Select(c => c.CategoryName).ToList(),
            ValuationReasoning = valuation.ValuationReasoning,
            FengShuiAnalysis = valuation.FengShuiAnalysis,
            ReferencedDocuments = ragChunks.Select(c => c.DocumentName).Distinct().ToList(),
            AiModelName = valuation.AiModelName,
            AppliedToSim = valuation.AppliedToSim,
            CreatedDate = valuation.CreatedDate
        };
    }
}
