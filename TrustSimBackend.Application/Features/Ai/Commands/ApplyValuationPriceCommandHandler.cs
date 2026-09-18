using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Ai.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Sim;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands;

public class ApplyValuationPriceCommandHandler : IRequestHandler<ApplyValuationPriceCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISimFeatureExtractor _featureExtractor;

    public ApplyValuationPriceCommandHandler(
        IUnitOfWork unitOfWork,
        ISimFeatureExtractor featureExtractor)
    {
        _unitOfWork = unitOfWork;
        _featureExtractor = featureExtractor;
    }

    public async Task<bool> Handle(ApplyValuationPriceCommand request, CancellationToken cancellationToken)
    {
        var valuation = await _unitOfWork.SimValuations.GetByIdAsync(request.ValuationId, cancellationToken);
        if (valuation == null)
        {
            throw new NotFoundException($"Không tìm thấy bản ghi định giá ID {request.ValuationId}");
        }

        var simNumber = await _unitOfWork.SimNumbers.GetByIdAsync(valuation.SimNumberId, cancellationToken);
        if (simNumber == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM tương ứng ID {valuation.SimNumberId}");
        }

        // 1. Cập nhật mức giá được AI đề xuất vào SimNumber
        simNumber.Price = valuation.SuggestedPrice;
        valuation.AppliedToSim = true;

        // 2. Bóc tách lại đặc trưng để xác định nhịp tối ưu và danh sách tổ hợp
        var carrierName = simNumber.PhonePrefix?.SimCarrier?.CarrierName;
        var prefixScore = simNumber.PhonePrefix?.PrefixReputationScore ?? 1.0f;
        var features = _featureExtractor.ExtractFeatures(simNumber.PhoneNumber, carrierName, prefixScore);

        // 3. Gán nhịp định dạng tối ưu vào simNumber.FormatRhythmId
        var rhythms = await _unitOfWork.FormatRhythms.GetAllAsync(cancellationToken);
        var matchingRhythm = rhythms.FirstOrDefault(r => r.Value == features.SelectedRhythmValue || r.Value == features.DetectedRhythmCode)
                             ?? rhythms.FirstOrDefault(r => r.FormatRhythmName.Contains(features.DetectedRhythmName))
                             ?? rhythms.FirstOrDefault(r => r.Value == "3.3.4" || r.Value == "NORMAL");

        if (matchingRhythm != null)
        {
            simNumber.FormatRhythmId = matchingRhythm.Id;
        }

        // 4. Xóa mapping tổ hợp cũ và tạo mới các bản ghi trong sim_category_mappings
        var existingMappings = await _unitOfWork.SimCategoryMappings.GetBySimNumberIdAsync(simNumber.Id, cancellationToken);
        if (existingMappings.Count > 0)
        {
            _unitOfWork.SimCategoryMappings.RemoveRange(existingMappings);
        }

        var newMappings = new List<SimCategoryMapping>();
        foreach (var catFeature in features.DetectedCategories)
        {
            var category = await _unitOfWork.SimCategories.GetByCodeAsync(catFeature.CategoryCode, cancellationToken);
            if (category != null)
            {
                newMappings.Add(new SimCategoryMapping
                {
                    SimNumberId = simNumber.Id,
                    SimCategoryId = category.Id,
                    ScoreContribution = catFeature.ScoreContribution,
                    IsActive = true
                });
            }
        }

        if (newMappings.Count > 0)
        {
            await _unitOfWork.SimCategoryMappings.AddRangeAsync(newMappings, cancellationToken);
        }

        _unitOfWork.SimNumbers.Update(simNumber);
        _unitOfWork.SimValuations.Update(valuation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
