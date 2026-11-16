using System.Collections.Concurrent;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Application.Features.Trade.Helpers;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler xử lý nộp bộ bằng chứng chuyển nhượng SIM (1 TransferProof chứa nhiều TransferProofImage),
/// tích hợp Idempotency Key chống gửi trùng và chặn nộp mới khi đang có bộ PENDING.
/// </summary>
public class SubmitMultipleTransferProofsCommandHandler :
    IRequestHandler<SubmitMultipleTransferProofsCommand, SimTransferDetailsDto>,
    IRequestHandler<SubmitMultipleTransferProofsByAuctionCommand, SimTransferDetailsDto>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> IdempotencyLocks = new();

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICloudStorageService _cloudStorageService;
    private readonly INotificationService _notificationService;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<SubmitMultipleTransferProofsCommandHandler> _logger;

    public SubmitMultipleTransferProofsCommandHandler(
        IUnitOfWork unitOfWork,
        ICloudStorageService cloudStorageService,
        INotificationService notificationService,
        IAuctionDelayQueueService delayQueue,
        IOptions<TradeDeadlineSettings> deadlineOptions,
        ILogger<SubmitMultipleTransferProofsCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _cloudStorageService = cloudStorageService;
        _notificationService = notificationService;
        _delayQueue = delayQueue;
        _deadlineSettings = deadlineOptions.Value;
        _logger = logger;
    }

    public async Task<SimTransferDetailsDto> Handle(SubmitMultipleTransferProofsCommand request, CancellationToken cancellationToken)
    {
        var validFiles = FilterValidFiles(request.Files);
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        return await ProcessProofsWithIdempotencyAsync(
            trade,
            request.UserId,
            validFiles,
            request.ProofType,
            request.Description,
            request.IdempotencyKey,
            cancellationToken);
    }

    public async Task<SimTransferDetailsDto> Handle(SubmitMultipleTransferProofsByAuctionCommand request, CancellationToken cancellationToken)
    {
        var validFiles = FilterValidFiles(request.Files);
        var trade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(request.AuctionId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng cho phiên đấu giá '{request.AuctionId}'.");
        }

        return await ProcessProofsWithIdempotencyAsync(
            trade,
            request.UserId,
            validFiles,
            request.ProofType,
            request.Description,
            request.IdempotencyKey,
            cancellationToken);
    }

    private static List<IFormFile> FilterValidFiles(List<IFormFile>? files)
    {
        var validFiles = files?.Where(f => f != null && f.Length > 0).ToList() ?? new List<IFormFile>();
        if (validFiles.Count == 0)
        {
            throw new BadRequestException("Vui lòng tải lên ít nhất một hình ảnh bằng chứng.");
        }

        return validFiles;
    }

    private async Task<SimTransferDetailsDto> ProcessProofsWithIdempotencyAsync(
        TradeTransaction trade,
        Guid userId,
        List<IFormFile> files,
        TransferProofType proofType,
        string? description,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        ValidateSubmitterPermission(trade, userId, proofType);

        var cleanKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        if (cleanKey == null)
        {
            return await CreateAndSaveProofAsync(trade, userId, files, proofType, description, null, cancellationToken);
        }

        var lockKey = $"{trade.AuctionId:N}:{cleanKey}";
        var keyLock = IdempotencyLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(cancellationToken);
        try
        {
            var existingProof = await _unitOfWork.TradeTransactions.GetTransferProofByIdempotencyKeyAsync(
                trade.AuctionId,
                cleanKey,
                cancellationToken);

            if (existingProof != null)
            {
                _logger.LogInformation(
                    "[SimTransfer] Bỏ qua request trùng lặp theo IdempotencyKey '{Key}' cho Auction {AuctionId}",
                    cleanKey, trade.AuctionId);

                var latestTrade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(trade.AuctionId, cancellationToken) ?? trade;
                return SimTransferMappingHelper.BuildSimTransferDetailsDto(
                    latestTrade,
                    userId,
                    isAdmin: false,
                    message: "Yêu cầu nộp bằng chứng đã được ghi nhận trước đó (Idempotent).");
            }

            return await CreateAndSaveProofAsync(trade, userId, files, proofType, description, cleanKey, cancellationToken);
        }
        catch (Exception)
        {
            var existingProof = await _unitOfWork.TradeTransactions.GetTransferProofByIdempotencyKeyAsync(
                trade.AuctionId,
                cleanKey,
                cancellationToken);

            if (existingProof == null)
            {
                throw;
            }

            var latestTrade = await _unitOfWork.TradeTransactions.GetByAuctionIdAsync(trade.AuctionId, cancellationToken) ?? trade;
            return SimTransferMappingHelper.BuildSimTransferDetailsDto(
                latestTrade,
                userId,
                isAdmin: false,
                message: "Yêu cầu nộp bằng chứng đã được ghi nhận trước đó (Idempotent).");
        }
        finally
        {
            keyLock.Release();
        }
    }

    private static void ValidateSubmitterPermission(TradeTransaction trade, Guid userId, TransferProofType proofType)
    {
        if (proofType == TransferProofType.SELLER_HANDOVER)
        {
            if (trade.SellerId != userId)
            {
                throw new ForbiddenAccessException("Chỉ có Người Bán mới có quyền nộp biên bản sang tên SIM.");
            }
        }
        else if (trade.BuyerId != userId)
        {
            throw new ForbiddenAccessException("Chỉ có Người Mua mới có quyền nộp bằng chứng nghiệm thu SIM.");
        }
    }

    private async Task<SimTransferDetailsDto> CreateAndSaveProofAsync(
        TradeTransaction trade,
        Guid userId,
        List<IFormFile> files,
        TransferProofType proofType,
        string? description,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var isSeller = proofType == TransferProofType.SELLER_HANDOVER;

        if (trade.SimTransfer == null)
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
            await _unitOfWork.TradeTransactions.AddSimTransferAsync(newSimTransfer, cancellationToken);
            trade.SimTransfer = newSimTransfer;
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.COMPLETED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã hoàn tất thành công trước đó.");
        }

        if (trade.SimTransfer.SimTransferStatus == SimTransferStatus.CANCELLED)
        {
            throw new BadRequestException("Giao dịch chuyển nhượng SIM này đã bị hủy bỏ.");
        }

        var existingProofs = SimTransferMappingHelper.GetMergedProofs(trade);

        var hasApprovedSellerProof = existingProofs
            .Any(p => p.ProofType == TransferProofType.SELLER_HANDOVER && p.ProofStatus == ProofStatus.APPROVED);
        var hasApprovedBuyerProof = existingProofs
            .Any(p => p.ProofType == TransferProofType.BUYER_RECEIPT && p.ProofStatus == ProofStatus.APPROVED);

        if (isSeller)
        {
            if (hasApprovedSellerProof)
            {
                throw new BadRequestException("Bằng chứng sang tên của Người Bán đã được Ban Quản Trị phê duyệt, không thể nộp lại.");
            }
        }
        else
        {
            if (!hasApprovedSellerProof)
            {
                throw new BadRequestException("Bằng chứng chuyển nhượng của Người Bán phải được Ban Quản Trị sàn phê duyệt thành công trước khi Người Mua nộp bằng chứng nhận SIM.");
            }

            if (hasApprovedBuyerProof)
            {
                throw new BadRequestException("Bằng chứng nghiệm thu của Người Mua đã được Ban Quản Trị phê duyệt, không thể nộp lại.");
            }
        }

        var hasPendingSameType = existingProofs.Any(p => p.ProofType == proofType && p.ProofStatus == ProofStatus.PENDING)
            || await _unitOfWork.TradeTransactions.HasPendingTransferProofAsync(trade.AuctionId, proofType, cancellationToken);

        if (hasPendingSameType)
        {
            throw new BadRequestException("Hiện đang có bộ bằng chứng chờ duyệt. Vui lòng thu hồi bộ bằng chứng đang chờ trước khi tải lên bằng chứng mới.");
        }

        var proof = new TransferProof
        {
            Id = Guid.NewGuid(),
            AuctionId = trade.AuctionId,
            SimTransferId = trade.SimTransfer.Id,
            SubmittedByUserId = userId,
            Description = description?.Trim(),
            ProofStatus = ProofStatus.PENDING,
            ProofType = proofType,
            VerifiedAt = null,
            IdempotencyKey = idempotencyKey
        };

        var sortOrder = 0;
        foreach (var file in files)
        {
            var uploadedUrl = await _cloudStorageService.UploadImageAsync(file, "sim-transfer", cancellationToken);
            var image = new TransferProofImage
            {
                Id = Guid.NewGuid(),
                TransferProofId = proof.Id,
                ImageUrl = uploadedUrl,
                FileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                SortOrder = sortOrder++
            };

            await _unitOfWork.TradeTransactions.AddTransferProofImageAsync(image, cancellationToken);
            proof.AddImage(image);
        }

        await _unitOfWork.TradeTransactions.AddTransferProofAsync(proof, cancellationToken);
        trade.SimTransfer.AddTransferProof(proof);
        trade.Auction?.AddTransferProof(proof);

        if (isSeller)
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.SELLER_PROOF_SUBMITTED;

            var liqHours = _deadlineSettings.LiquidationSigningDeadlineHours > 0
                ? _deadlineSettings.LiquidationSigningDeadlineHours
                : 72;
            var liquidationDeadline = DateTime.UtcNow.AddHours(liqHours);
            trade.LiquidationDeadline = liquidationDeadline;

            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = trade.AuctionId,
                JobType = AuctionDelayJobTypes.LIQUIDATION_DEADLINE,
                ExecuteAt = liquidationDeadline,
                ExtraData = trade.Id.ToString()
            }, cancellationToken);

            _logger.LogInformation(
                "[SimTransfer] Người bán {SellerId} đã nộp bộ bằng chứng ({Count} ảnh) cho SIM {SimNumber} (Giao dịch: {TradeCode})",
                trade.SellerId, files.Count, trade.SimNumber?.PhoneNumber, trade.TradeTransactionCode);

            await _notificationService.SendNotificationAsync(
                trade.BuyerId,
                NotificationType.TRADE_TRANSFERRING_SIM,
                "Người bán đã nộp biên bản sang tên SIM",
                $"Người bán đã nộp {files.Count} ảnh biên bản sang tên cho SIM {trade.SimNumber?.PhoneNumber}. Vui lòng chờ Ban Quản Trị sàn kiểm tra và duyệt hồ sơ.",
                $"/trade/{trade.Id}",
                cancellationToken: cancellationToken);
        }
        else
        {
            trade.SimTransfer.SimTransferStatus = SimTransferStatus.BUYER_PROOF_SUBMITTED;

            _logger.LogInformation(
                "[SimTransfer] Người mua {BuyerId} đã nộp bộ bằng chứng ({Count} ảnh) nghiệm thu SIM {SimNumber} (Giao dịch: {TradeCode})",
                trade.BuyerId, files.Count, trade.SimNumber?.PhoneNumber, trade.TradeTransactionCode);

            await _notificationService.SendNotificationAsync(
                trade.SellerId,
                NotificationType.TRADE_TRANSFERRING_SIM,
                "Người mua đã nộp ảnh nghiệm thu SIM",
                $"Người mua đã nộp {files.Count} ảnh kiểm tra sóng / nghiệm thu cho SIM {trade.SimNumber?.PhoneNumber}. Đang chờ Ban Quản Trị sàn duyệt hoàn tất và giải ngân.",
                $"/trade/{trade.Id}",
                cancellationToken: cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var message = isSeller
            ? $"Đã tải lên bộ bằng chứng gồm {files.Count} ảnh biên bản sang tên thành công. Chờ Admin sàn duyệt hồ sơ."
            : $"Đã tải lên bộ bằng chứng gồm {files.Count} ảnh kiểm tra nghiệm thu thành công. Chờ Admin sàn duyệt và giải ngân.";

        return SimTransferMappingHelper.BuildSimTransferDetailsDto(trade, userId, isAdmin: false, message);
    }
}
