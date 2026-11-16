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
/// Handler xử lý lấy thông tin chi tiết tiến trình bàn giao SIM và giải ngân Escrow.
/// </summary>
public class GetSimTransferDetailsQueryHandler : 
    IRequestHandler<GetSimTransferDetailsQuery, SimTransferDetailsDto>,
    IRequestHandler<GetSimTransferDetailsByAuctionQuery, SimTransferDetailsDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfContractService _pdfContractService;
    private readonly IPdfSigningService _pdfSigningService;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly ILogger<GetSimTransferDetailsQueryHandler> _logger;

    public GetSimTransferDetailsQueryHandler(
        IUnitOfWork unitOfWork,
        IPdfContractService pdfContractService,
        IPdfSigningService pdfSigningService,
        ICloudStorageService cloudStorageService,
        ILogger<GetSimTransferDetailsQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfContractService = pdfContractService;
        _pdfSigningService = pdfSigningService;
        _cloudStorageService = cloudStorageService;
        _logger = logger;
    }

    public async Task<SimTransferDetailsDto> Handle(GetSimTransferDetailsQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        return await ProcessDetailsInternalAsync(trade, request.UserId, request.IsAdmin, cancellationToken);
    }

    public async Task<SimTransferDetailsDto> Handle(GetSimTransferDetailsByAuctionQuery request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(request.AuctionId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng cho phiên đấu giá '{request.AuctionId}'.");
        }

        return await ProcessDetailsInternalAsync(trade, request.UserId, request.IsAdmin, cancellationToken);
    }

    private async Task<SimTransferDetailsDto> ProcessDetailsInternalAsync(
        TradeTransaction trade,
        Guid userId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var isBuyer = trade.BuyerId == userId;
        var isSeller = trade.SellerId == userId;

        if (!isBuyer && !isSeller && !isAdmin)
        {
            throw new ForbiddenAccessException("Bạn không có quyền truy cập thông tin bàn giao SIM của giao dịch này.");
        }

        var needsSave = false;

        // Tự động kiểm tra và khởi tạo SimTransfer nếu cả 2 bên đã hoàn tất ký hợp đồng
        var buyerSig = trade.TransferContract?.DigitalSignatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = trade.TransferContract?.DigitalSignatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);
        var isFullySigned = buyerSig != null && (sellerSig != null || (trade.TransferContract?.IsAutoEnforced ?? false));

        if (isFullySigned && trade.SimTransfer == null)
        {
            var now = DateTime.UtcNow;
            var newSimTransfer = new SimTransfer
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                StartAt = now,
                Deadline = now.AddDays(7),
                SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
            };

            if (trade.TransferContract != null && trade.TransferContract.ContractStatus == ContractStatus.DRAFT)
            {
                trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
            }

            if (trade.TradeTransactionStatus == TradeTransactionStatus.PendingSignature)
            {
                trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;
            }

            await _unitOfWork.TradeTransactions.AddSimTransferAsync(newSimTransfer, cancellationToken);
            trade.SimTransfer = newSimTransfer;
            needsSave = true;
        }

        // Tự động sinh bù Văn bản số 3 nếu giao dịch đã hoàn tất mà chưa có URL
        var isCompleted = trade.TradeTransactionStatus == TradeTransactionStatus.Completed;
        if (isCompleted && string.IsNullOrWhiteSpace(trade.TransferContract?.LiquidationDocumentUrl))
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
                    _logger.LogWarning(uploadEx, "[SimTransferDetails] Fallback lưu local file cho {TradeCode}", trade.TradeTransactionCode);
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
                needsSave = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SimTransferDetails] ⚠️ Lỗi trong On-Demand Auto-Seal Biên bản thanh lý cho {TradeCode}", trade.TradeTransactionCode);
            }
        }

        if (needsSave)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return SimTransferMappingHelper.BuildSimTransferDetailsDto(
            trade,
            userId,
            isAdmin,
            message: "Lấy thông tin tiến trình bàn giao SIM thành công.");
    }
}
