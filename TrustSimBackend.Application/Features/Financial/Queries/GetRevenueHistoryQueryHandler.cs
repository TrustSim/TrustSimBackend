using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Handler xử lý truy vấn lịch sử biến động doanh thu sàn (hoa hồng, tiền phạt và các lần Admin rút tiền).
/// </summary>
public class GetRevenueHistoryQueryHandler : IRequestHandler<GetRevenueHistoryQuery, PagedResult<PlatformRevenueHistoryDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    private static readonly HashSet<LedgerTransactionType> RevenueTransactionTypes = new()
    {
        LedgerTransactionType.ESCROW_RELEASE,
        LedgerTransactionType.AUCTION_DEPOSIT_FORFEITED,
        LedgerTransactionType.ADMIN_PROFIT_WITHDRAW
    };

    public GetRevenueHistoryQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<PlatformRevenueHistoryDto>> Handle(GetRevenueHistoryQuery request, CancellationToken cancellationToken)
    {
        var ledgerFilter = new LedgerJournalQueryFilter
        {
            TransactionType = request.Filter.TransactionType,
            FromDate = request.Filter.FromDate,
            ToDate = request.Filter.ToDate,
            Keyword = request.Filter.Keyword,
            SortBy = request.Filter.SortBy,
            SortOrder = request.Filter.SortOrder,
            PageIndex = request.Filter.PageIndex,
            PageSize = request.Filter.PageSize
        };

        var (journals, totalCount) = await _unitOfWork.LedgerJournals.GetPagedAsync(
            ledgerFilter,
            cancellationToken);

        // Lọc các chứng từ liên quan đến doanh thu nếu không chỉ định loại cụ thể
        var revenueJournals = request.Filter.TransactionType.HasValue
            ? journals
            : journals.Where(j => RevenueTransactionTypes.Contains(j.TransactionType)).ToList();

        var dtos = new List<PlatformRevenueHistoryDto>();

        foreach (var journal in revenueJournals)
        {
            decimal revenueAmount = 0;

            // Xác định số tiền biến động doanh thu của chứng từ
            if (journal.TransactionType == LedgerTransactionType.ESCROW_RELEASE)
            {
                // Tiền phí sàn 5% từ dòng CREDIT PLATFORM_FEE_REVENUE
                revenueAmount = journal.Entries
                    .Where(e => e.AccountType == FinancialAccountType.PLATFORM_FEE_REVENUE && e.EntryType == LedgerEntryType.CREDIT)
                    .Sum(e => e.Amount);
            }
            else if (journal.TransactionType == LedgerTransactionType.AUCTION_DEPOSIT_FORFEITED)
            {
                // Tiền phạt 10% từ dòng CREDIT PLATFORM_PENALTY_REVENUE
                revenueAmount = journal.Entries
                    .Where(e => e.AccountType == FinancialAccountType.PLATFORM_PENALTY_REVENUE && e.EntryType == LedgerEntryType.CREDIT)
                    .Sum(e => e.Amount);
            }
            else if (journal.TransactionType == LedgerTransactionType.ADMIN_PROFIT_WITHDRAW)
            {
                // Số tiền Admin đã rút (hiển thị số âm để thể hiện tiền rời khỏi quỹ doanh thu)
                revenueAmount = -journal.TotalAmount;
            }
            else
            {
                revenueAmount = journal.TotalAmount;
            }

            var typeName = journal.TransactionType switch
            {
                LedgerTransactionType.ESCROW_RELEASE => "Thu phí hoa hồng dịch vụ bán SIM",
                LedgerTransactionType.AUCTION_DEPOSIT_FORFEITED => "Thu tiền phạt người chơi bỏ cọc đấu giá",
                LedgerTransactionType.ADMIN_PROFIT_WITHDRAW => "Admin rút lợi nhuận sàn về tài khoản cá nhân",
                _ => journal.TransactionType.ToString()
            };

            dtos.Add(new PlatformRevenueHistoryDto
            {
                JournalId = journal.Id,
                JournalCode = journal.JournalCode,
                TransactionType = journal.TransactionType,
                TransactionTypeName = typeName,
                Amount = revenueAmount,
                Description = journal.Description ?? string.Empty,
                ReferenceType = journal.ReferenceType,
                ReferenceId = journal.ReferenceId,
                SignedHash = journal.SignedHash,
                PostedAt = journal.PostedAt
            });
        }

        return new PagedResult<PlatformRevenueHistoryDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
