using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Ai.Commands.ApplyValuationPrice;

public record ApplyValuationPriceCommand(
    Guid ValuationId,
    Guid CurrentUserId
) : IRequest<bool>;

public class ApplyValuationPriceCommandHandler : IRequestHandler<ApplyValuationPriceCommand, bool>
{
    private readonly ISimValuationRepository _valuationRepository;
    private readonly ISimNumberRepository _simNumberRepository;
    private readonly IFormatRhythmRepository _rhythmRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApplyValuationPriceCommandHandler(
        ISimValuationRepository valuationRepository,
        ISimNumberRepository simNumberRepository,
        IFormatRhythmRepository rhythmRepository,
        IUnitOfWork unitOfWork)
    {
        _valuationRepository = valuationRepository;
        _simNumberRepository = simNumberRepository;
        _rhythmRepository = rhythmRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(ApplyValuationPriceCommand request, CancellationToken cancellationToken)
    {
        var valuation = await _valuationRepository.GetByIdAsync(request.ValuationId, cancellationToken);
        if (valuation == null)
        {
            throw new NotFoundException($"Không tìm thấy bản ghi định giá ID {request.ValuationId}");
        }

        var simNumber = await _simNumberRepository.GetByIdAsync(valuation.SimNumberId, cancellationToken);
        if (simNumber == null)
        {
            throw new NotFoundException($"Không tìm thấy số SIM tương ứng ID {valuation.SimNumberId}");
        }

        // Cập nhật mức giá được AI đề xuất vào SimNumber
        simNumber.Price = valuation.SuggestedPrice;
        valuation.AppliedToSim = true;

        _simNumberRepository.Update(simNumber);
        _valuationRepository.Update(valuation);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
