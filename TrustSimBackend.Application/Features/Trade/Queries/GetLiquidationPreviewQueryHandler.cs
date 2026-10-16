using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

/// <summary>
/// Handler xử lý sinh nội dung xem trước Biên bản nghiệm thu và thanh lý giao dịch điện tử.
/// </summary>
public class GetLiquidationPreviewQueryHandler : IRequestHandler<GetLiquidationPreviewQuery, LiquidationPreviewDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfContractService _pdfContractService;
    private readonly ILogger<GetLiquidationPreviewQueryHandler> _logger;

    public GetLiquidationPreviewQueryHandler(
        IUnitOfWork unitOfWork,
        IPdfContractService pdfContractService,
        ILogger<GetLiquidationPreviewQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfContractService = pdfContractService;
        _logger = logger;
    }

    public async Task<LiquidationPreviewDto> Handle(GetLiquidationPreviewQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        var isBuyer = trade.BuyerId == request.CurrentUserId;
        var isSeller = trade.SellerId == request.CurrentUserId;

        if (!isBuyer && !isSeller && !request.IsAdmin)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem biên bản thanh lý của giao dịch này.");
        }

        var htmlPreview = await _pdfContractService.GetLiquidationPreviewHtmlAsync(trade.Id, request.CurrentUserId, cancellationToken);
        var contract = trade.TransferContract;
        var isCompleted = trade.TradeTransactionStatus == TradeTransactionStatus.Completed;

        return new LiquidationPreviewDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            HtmlContent: htmlPreview,
            LiquidationDocumentUrl: contract?.LiquidationDocumentUrl,
            LiquidationDocumentHash: contract?.LiquidationDocumentHash,
            LiquidationSealedAt: contract?.LiquidationSealedAt,
            IsCompleted: isCompleted,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber ?? "Chưa xác định",
            EscrowAmount: trade.Escrow?.EscrowAmount ?? 0m,
            BuyerName: trade.Buyer?.UserProfile?.FullName ?? trade.Buyer?.UserName ?? "Bên Mua",
            SellerName: trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName ?? "Bên Bán",
            TransferDeadline: trade.TransferDeadline,
            LiquidationDeadline: trade.LiquidationDeadline
        );
    }
}
