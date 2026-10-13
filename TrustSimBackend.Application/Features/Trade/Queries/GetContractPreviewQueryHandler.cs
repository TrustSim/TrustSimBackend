using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Queries;

/// <summary>
/// Handler xử lý sinh nội dung xem trước hợp đồng và kiểm tra trạng thái ký của Bên Mua / Bên Bán.
/// </summary>
public class GetContractPreviewQueryHandler : IRequestHandler<GetContractPreviewQuery, ContractPreviewDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfContractService _pdfContractService;

    public GetContractPreviewQueryHandler(
        IUnitOfWork unitOfWork,
        IPdfContractService pdfContractService)
    {
        _unitOfWork = unitOfWork;
        _pdfContractService = pdfContractService;
    }

    public async Task<ContractPreviewDto> Handle(GetContractPreviewQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        var isBuyer = trade.BuyerId == request.CurrentUserId;
        var isSeller = trade.SellerId == request.CurrentUserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem thông tin hợp đồng của giao dịch này.");
        }

        var htmlPreview = await _pdfContractService.GetContractPreviewHtmlAsync(trade.Id, request.CurrentUserId, cancellationToken);

        var signatures = trade.TransferContract?.DigitalSignatures;
        var buyerSig = signatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = signatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);

        var isFullySigned = buyerSig != null && (sellerSig != null || (trade.TransferContract?.IsAutoEnforced ?? false));

        // Tự động đồng bộ trạng thái FULLY_SIGNED và khởi tạo SimTransfer nếu cả 2 bên đã hoàn tất ký xác nhận
        if (isFullySigned && trade.TransferContract != null && trade.TransferContract.ContractStatus == ContractStatus.DRAFT)
        {
            trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
            trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;

            if (trade.SimTransfer == null)
            {
                var simTransfer = new SimTransfer
                {
                    Id = Guid.NewGuid(),
                    TradeTransactionId = trade.Id,
                    StartAt = DateTime.UtcNow,
                    Deadline = DateTime.UtcNow.AddDays(7),
                    SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
                };
                await _unitOfWork.TradeTransactions.AddSimTransferAsync(simTransfer, cancellationToken);
                trade.SimTransfer = simTransfer;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ContractPreviewDto(
            TradeId: trade.Id,
            TradeTransactionCode: trade.TradeTransactionCode,
            HtmlContent: htmlPreview,
            BuyerSigned: buyerSig != null,
            BuyerSignedAt: buyerSig?.SignedAt,
            SellerSigned: sellerSig != null,
            SellerSignedAt: sellerSig?.SignedAt,
            IsFullySigned: isFullySigned,
            ContractStatus: (trade.TransferContract?.ContractStatus ?? ContractStatus.DRAFT).ToString(),
            DocumentPdfUrl: string.IsNullOrWhiteSpace(trade.TransferContract?.DocumentUrl) ? null : trade.TransferContract.DocumentUrl,
            SigningDeadline: trade.SigningDeadline,
            DocumentHash: trade.TransferContract?.DocumentHash,
            PadesSignatureSerial: trade.TransferContract?.PadesSignatureSerial,
            PadesSignatureHash: trade.TransferContract?.PadesSignatureHash,
            TsaTimestamp: trade.TransferContract?.TsaTimestamp,
            TsaAuthority: trade.TransferContract?.TsaAuthority,
            IsAutoEnforced: trade.TransferContract?.IsAutoEnforced ?? false,
            SealedAt: trade.TransferContract?.SealedAt,
            BuyerIp: buyerSig?.IpAddress,
            BuyerAuthMethod: buyerSig?.AuthMethod,
            SellerIp: sellerSig?.IpAddress,
            SellerAuthMethod: sellerSig?.AuthMethod
        );
    }
}
