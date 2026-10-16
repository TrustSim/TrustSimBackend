using System.Security.Cryptography;
using MediatR;
using Microsoft.Extensions.Logging;
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
    private readonly IPdfSigningService _pdfSigningService;
    private readonly ICloudStorageService _cloudStorage;
    private readonly ILogger<GetContractPreviewQueryHandler> _logger;

    public GetContractPreviewQueryHandler(
        IUnitOfWork unitOfWork,
        IPdfContractService pdfContractService,
        IPdfSigningService pdfSigningService,
        ICloudStorageService cloudStorage,
        ILogger<GetContractPreviewQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _pdfContractService = pdfContractService;
        _pdfSigningService = pdfSigningService;
        _cloudStorage = cloudStorage;
        _logger = logger;
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

        if (!isBuyer && !isSeller && !request.IsAdmin)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem thông tin hợp đồng của giao dịch này.");
        }

        var htmlPreview = await _pdfContractService.GetContractPreviewHtmlAsync(trade.Id, request.CurrentUserId, cancellationToken);

        var signatures = trade.TransferContract?.DigitalSignatures;
        var buyerSig = signatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.BUYER);
        var sellerSig = signatures?.FirstOrDefault(s => s.SignatureRole == SignatureRole.SELLER);

        var isFullySigned = buyerSig != null && (sellerSig != null || (trade.TransferContract?.IsAutoEnforced ?? false));

        // Tự động đồng bộ trạng thái FULLY_SIGNED, khởi tạo SimTransfer và niêm phong On-Demand nếu cần
        if (isFullySigned && trade.TransferContract != null)
        {
            var needsSave = false;

            if (trade.TransferContract.ContractStatus == ContractStatus.DRAFT)
            {
                trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
                trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;
                needsSave = true;
            }

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
                needsSave = true;
            }

            // Cơ chế On-Demand Auto-Seal: Tự động kích hoạt nếu chưa có file hoặc đang mang đường dẫn local cũ
            var docUrl = trade.TransferContract.DocumentUrl;
            var needsCloudUpload = string.IsNullOrWhiteSpace(docUrl) 
                || string.IsNullOrWhiteSpace(trade.TransferContract.DocumentHash)
                || docUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                || docUrl.Contains("95111fd91d6548f7a246ad90ed1909b1", StringComparison.OrdinalIgnoreCase);

            if (needsCloudUpload)
            {
                try
                {
                    _logger.LogInformation("[ContractPreview] 🔄 Kích hoạt Auto-Seal On-Demand cho hợp đồng {TradeCode}...", trade.TradeTransactionCode);
                    var isAuto = trade.TransferContract.IsAutoEnforced;
                    var rawPdfBytes = await _pdfContractService.GenerateRawPdfBytesAsync(trade.Id, isAuto, cancellationToken);
                    var stampBoxes = await _pdfContractService.GetStampBoxesAsync(trade.Id, isAuto, cancellationToken);

                    var signResult = await _pdfSigningService.SignPdfPadesAsync(
                        rawPdfBytes,
                        "Xác nhận chuyển nhượng quyền sử dụng số thuê bao viễn thông",
                        "Hà Nội, Việt Nam",
                        stampBoxes,
                        cancellationToken);

                    var simPhone = trade.SimNumber?.PhoneNumber?.Replace("+", "") ?? "SIM";
                    var fileName = $"HopDong_{trade.TradeTransactionCode}_{simPhone}_Signed.pdf";
                    var fileUrl = await _cloudStorage.UploadImageBytesAsync(signResult.SignedPdfBytes, fileName, "contracts", "application/pdf", cancellationToken);

                    trade.TransferContract.DocumentUrl = fileUrl;
                    trade.TransferContract.DocumentHash = signResult.Sha256Hash;
                    trade.TransferContract.PadesSignatureSerial = signResult.CertificateSerial;
                    trade.TransferContract.PadesSignatureHash = signResult.Sha256Hash;
                    trade.TransferContract.TsaTimestamp = signResult.TsaTimestamp;
                    trade.TransferContract.TsaAuthority = signResult.TsaAuthority;
                    trade.TransferContract.SealedAt = signResult.SignedAt;
                    trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
                    trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;
                    needsSave = true;

                    _logger.LogInformation("[ContractPreview] ✅ Auto-Seal On-Demand hoàn tất cho {TradeCode}! Hash: {Hash}", trade.TradeTransactionCode, signResult.Sha256Hash);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ContractPreview] ⚠️ Lỗi trong Auto-Seal On-Demand cho {TradeCode}", trade.TradeTransactionCode);
                }
            }

            // Tự động sinh Văn bản 1: Phiếu xác nhận kết quả đấu giá / Phiếu chốt giá trúng điện tử nếu chưa có
            if (string.IsNullOrWhiteSpace(trade.TransferContract.WinningNoticeUrl))
            {
                try
                {
                    _logger.LogInformation("[ContractPreview] 📄 Tự động tạo Phiếu chốt giá trúng cho {TradeCode}...", trade.TradeTransactionCode);
                    var noticeBytes = await _pdfContractService.GenerateWinningNoticePdfAsync(trade.Id, cancellationToken);
                    var simPhone = trade.SimNumber?.PhoneNumber?.Replace("+", "") ?? "SIM";
                    var noticeFileName = $"PhieuChotGia_{trade.TradeTransactionCode}_{simPhone}.pdf";
                    var noticeUrl = await _cloudStorage.UploadImageBytesAsync(noticeBytes, noticeFileName, "contracts", "application/pdf", cancellationToken);

                    using var sha = SHA256.Create();
                    var noticeHash = Convert.ToHexString(sha.ComputeHash(noticeBytes)).ToLowerInvariant();

                    trade.TransferContract.WinningNoticeUrl = noticeUrl;
                    trade.TransferContract.WinningNoticeHash = noticeHash;
                    needsSave = true;
                    _logger.LogInformation("[ContractPreview] ✅ Đã lưu Phiếu chốt giá trúng cho {TradeCode}: {Url}", trade.TradeTransactionCode, noticeUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[ContractPreview] ⚠️ Lỗi khi tự động tạo Phiếu chốt giá trúng cho {TradeCode}", trade.TradeTransactionCode);
                }
            }

            if (needsSave)
            {
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
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
            SellerAuthMethod: sellerSig?.AuthMethod,
            SimPhoneNumber: trade.SimNumber?.PhoneNumber,
            EscrowAmount: trade.Escrow?.EscrowAmount,
            BuyerName: trade.Buyer?.UserProfile?.FullName ?? trade.Buyer?.UserName,
            SellerName: trade.Seller?.UserProfile?.FullName ?? trade.Seller?.UserName,
            WinningNoticeUrl: trade.TransferContract?.WinningNoticeUrl,
            WinningNoticeHash: trade.TransferContract?.WinningNoticeHash,
            LiquidationDocumentUrl: trade.TransferContract?.LiquidationDocumentUrl,
            LiquidationDocumentHash: trade.TransferContract?.LiquidationDocumentHash,
            LiquidationSealedAt: trade.TransferContract?.LiquidationSealedAt,
            TransferDeadline: trade.TransferDeadline,
            LiquidationDeadline: trade.LiquidationDeadline
        );
    }
}
