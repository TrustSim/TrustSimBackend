using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Query lấy danh sách chứng từ kế toán ghi kép (phân trang và bộ lọc).
/// </summary>
public record GetLedgerJournalsQuery(
    LedgerTransactionType? TransactionType = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageIndex = 1,
    int PageSize = 20) : IRequest<PagedResult<LedgerJournalDto>>;

public class GetLedgerJournalsQueryHandler : IRequestHandler<GetLedgerJournalsQuery, PagedResult<LedgerJournalDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetLedgerJournalsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<LedgerJournalDto>> Handle(GetLedgerJournalsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.LedgerJournals.GetPagedAsync(
            request.TransactionType,
            request.FromDate,
            request.ToDate,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(LedgerJournalDto.FromEntity).ToList();

        return new PagedResult<LedgerJournalDto>(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}
