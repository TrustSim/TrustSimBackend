using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

public class AdminExportTradesQueryHandler : IRequestHandler<AdminExportTradesQuery, byte[]>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IExcelService _excelService;

    public AdminExportTradesQueryHandler(IUnitOfWork unitOfWork, IExcelService excelService)
    {
        _unitOfWork = unitOfWork;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportTradesQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _unitOfWork.TradeTransactions.GetPagedAsync(
            request.Status,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

        var rows = items.Select(t => new TradeExcelExportRow
        {
            TransactionId = t.Id.ToString(),
            SimNumber = t.SimNumber?.PhoneNumber ?? string.Empty,
            BuyerName = t.Buyer?.UserProfile?.FullName ?? t.Buyer?.UserName ?? string.Empty,
            SellerName = t.Seller?.UserProfile?.FullName ?? t.Seller?.UserName ?? string.Empty,
            Price = t.Escrow?.EscrowAmount ?? 0m,
            DepositAmount = t.Escrow?.EscrowAmount ?? 0m,
            Status = t.TradeTransactionStatus.ToString(),
            CreatedDate = t.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Trades", cancellationToken: cancellationToken);
    }
}
