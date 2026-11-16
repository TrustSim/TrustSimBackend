using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Helpers;
using TrustSimBackend.Domain.Entities.Trade;
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
    private readonly IPdfSigningService _pdfSigningService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<GetLiquidationPreviewQueryHandler> _logger;

    public GetLiquidationPreviewQueryHandler(
        IUnitOfWork unitOfWork,
        IPdfContractService pdfContractService,
        IPdfSigningService pdfSigningService,
        ICloudStorageService cloudStorageService,
        ILogger<GetLiquidationPreviewQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfContractService = pdfContractService;
        _pdfSigningService = pdfSigningService;
        _cloudStorageService = cloudStorageService;
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

        var mergedProofs = SimTransferMappingHelper.GetMergedProofs(trade);
        var isCompleted = trade.TradeTransactionStatus == TradeTransactionStatus.Completed;
        var hasApprovedSeller = mergedProofs.Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);
        var hasApprovedBuyer = mergedProofs.Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.APPROVED);

        // Cơ chế Auto-Seal On-Demand cho Văn bản số 3 nếu giao dịch đã hoàn tất hoặc cả 2 bên đã duyệt mà chưa có link file
        if ((isCompleted || (hasApprovedSeller && hasApprovedBuyer)) && string.IsNullOrWhiteSpace(trade.TransferContract?.LiquidationDocumentUrl))
        {
            try
            {
                if (trade.TransferContract == null)
                {
                    trade.TransferContract = new TransferContract
                    {
                        Id = Guid.NewGuid(),
                        TradeTransactionId = trade.Id,
                        ContractStatus = ContractStatus.COMPLETED,
                        DocumentUrl = string.Empty,
                        DocumentHash = string.Empty
                    };
                }

                var rawLiqBytes = await _pdfContractService.GenerateLiquidationMinutePdfAsync(trade.Id, cancellationToken);
                var isAuto = trade.TransferContract.IsAutoEnforced;
                var stampBoxes = await _pdfContractService.GetStampBoxesAsync(trade.Id, isAuto, cancellationToken);

                var signResult = await _pdfSigningService.SignPdfPadesAsync(
                    rawLiqBytes,
                    "Nghiệm thu hoàn tất chuyển quyền sử dụng SIM và thanh lý giao dịch điện tử",
                    "Hà Nội, Việt Nam",
                    stampBoxes,
                    cancellationToken);

                var simPhone = trade.SimNumber?.PhoneNumber?.Replace("+", "") ?? "SIM";
                var liqFileName = $"BienBanThanhLy_{trade.TradeTransactionCode}_{simPhone}_Signed.pdf";
                string liqUrl;
                try
                {
                    liqUrl = await _cloudStorageService.UploadImageBytesAsync(
                        signResult.SignedPdfBytes,
                        liqFileName,
                        "contracts",
                        "application/pdf",
                        cancellationToken);
                }
                catch (Exception uploadEx)
                {
                    _logger.LogWarning(uploadEx, "[LiquidationPreview] Fallback lưu local file cho {TradeCode}", trade.TradeTransactionCode);
                    var localDir = Path.Combine(AppContext.BaseDirectory, "uploads", "contracts");
                    Directory.CreateDirectory(localDir);
                    var localPath = Path.Combine(localDir, liqFileName);
                    await File.WriteAllBytesAsync(localPath, signResult.SignedPdfBytes, cancellationToken);
                    liqUrl = $"/uploads/contracts/{liqFileName}";
                }

                trade.TransferContract.LiquidationDocumentUrl = liqUrl;
                trade.TransferContract.LiquidationDocumentHash = signResult.Sha256Hash;
                trade.TransferContract.LiquidationSealedAt = signResult.SignedAt;
                trade.TransferContract.ContractStatus = ContractStatus.COMPLETED;
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LiquidationPreview] ⚠️ Lỗi trong On-Demand Auto-Seal Biên bản thanh lý cho {TradeCode}", trade.TradeTransactionCode);
            }
        }

        var htmlPreview = await _pdfContractService.GetLiquidationPreviewHtmlAsync(trade.Id, request.CurrentUserId, cancellationToken);
        var contract = trade.TransferContract;

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
