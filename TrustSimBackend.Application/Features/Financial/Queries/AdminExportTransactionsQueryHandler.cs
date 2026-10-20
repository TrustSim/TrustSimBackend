using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

public class AdminExportTransactionsQueryHandler : IRequestHandler<AdminExportTransactionsQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public AdminExportTransactionsQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportTransactionsQuery request, CancellationToken cancellationToken)
    {
        var exportFilter = request.Filter ?? new AdminWalletTransactionsFilter();
        exportFilter.PageIndex = 1;
        exportFilter.PageSize = 100000;

        var (items, _) = await _unitOfWork.WalletTransactions.GetPagedAllAsync(
            exportFilter,
            cancellationToken: cancellationToken);

        var rows = items.Select(t => new TransactionExcelExportRow
        {
            TransactionCode = t.TransactionCode,
            UserName = t.Wallet?.UserAccount?.UserName ?? string.Empty,
            Type = t.WalletTransactionType.ToString(),
            Amount = t.Amount,
            Status = t.TransactionStatus.ToString(),
            Description = t.Description ?? string.Empty,
            CreatedDate = t.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Transactions", cancellationToken: cancellationToken);
    }
}
