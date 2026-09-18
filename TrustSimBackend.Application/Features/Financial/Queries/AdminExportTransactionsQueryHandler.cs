using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
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
        var (items, _) = await _unitOfWork.WalletTransactions.GetPagedAllAsync(
            request.Type,
            request.Status,
            pageIndex: 1,
            pageSize: 100000,
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
