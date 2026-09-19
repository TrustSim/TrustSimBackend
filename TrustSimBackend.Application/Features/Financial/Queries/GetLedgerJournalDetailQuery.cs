using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Query lấy thông tin chi tiết một chứng từ kế toán ghi kép kèm danh sách các dòng Nợ/Có.
/// </summary>
public record GetLedgerJournalDetailQuery(Guid JournalId) : IRequest<LedgerJournalDto>;

public class GetLedgerJournalDetailQueryHandler : IRequestHandler<GetLedgerJournalDetailQuery, LedgerJournalDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetLedgerJournalDetailQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<LedgerJournalDto> Handle(GetLedgerJournalDetailQuery request, CancellationToken cancellationToken)
    {
        var journal = await _unitOfWork.LedgerJournals.GetByIdAsync(request.JournalId, cancellationToken);
        if (journal == null)
        {
            throw new NotFoundException($"Không tìm thấy chứng từ kế toán với ID '{request.JournalId}'.");
        }

        return LedgerJournalDto.FromEntity(journal);
    }
}
