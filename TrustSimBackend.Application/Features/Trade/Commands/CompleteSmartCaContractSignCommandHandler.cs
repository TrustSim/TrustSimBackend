using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.Services;
using TrustSimBackend.Application.Features.Trade.DTOs.Requests;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Trade;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Trade.Commands;

/// <summary>
/// Handler hoàn tất ký số hợp đồng bằng VNPT SmartCA (dùng chung cho cả API Polling kiểm tra trạng thái và Webhook Callback từ VNPT SmartCA).
/// Đảm bảo tính Idempotent tuyệt đối khi Webhook và Polling gọi đồng thời.
/// </summary>
public class CompleteSmartCaContractSignCommandHandler : IRequestHandler<CompleteSmartCaContractSignCommand, SignContractResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmartCaService _smartCaService;
    private readonly IContractSigningQueueService _signingQueue;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<CompleteSmartCaContractSignCommandHandler> _logger;

    public CompleteSmartCaContractSignCommandHandler(
        IUnitOfWork unitOfWork,
        ISmartCaService smartCaService,
        IContractSigningQueueService signingQueue,
        IAuctionDelayQueueService delayQueue,
        IOptions<TradeDeadlineSettings> deadlineSettingsOptions,
        ILogger<CompleteSmartCaContractSignCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _smartCaService = smartCaService;
        _signingQueue = signingQueue;
        _delayQueue = delayQueue;
        _deadlineSettings = deadlineSettingsOptions.Value;
        _logger = logger;
    }

    public async Task<SignContractResultDto> Handle(
        CompleteSmartCaContractSignCommand request,
        CancellationToken cancellationToken)
    {
        if (request.WebhookStatusCode.HasValue && request.WebhookStatusCode.Value != 200)
        {
            throw new BadRequestException($"Giao dịch ký số VNPT SmartCA không thành công hoặc đã bị người dùng từ chối (StatusCode: {request.WebhookStatusCode.Value}).");
        }

        var session = SmartCaPendingSessionStore.TryGet(request.TranCodeOrTransactionId);
        var tradeId = request.TradeId ?? session?.TradeId
            ?? throw new NotFoundException($"Không tìm thấy phiên giao dịch ký số SmartCA ứng với mã '{request.TranCodeOrTransactionId}'.");

        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(tradeId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{tradeId}'.");

        var userId = request.UserId ?? session?.UserId ?? trade.BuyerId;
        var isBuyer = trade.BuyerId == userId;
        var isSeller = trade.SellerId == userId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Người dùng không phải là Bên Mua hoặc Bên Bán của giao dịch này.");
        }

        var role = isBuyer ? SignatureRole.BUYER : SignatureRole.SELLER;

        if (trade.TransferContract == null)
        {
            var newContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.DRAFT
            };
            await _unitOfWork.TradeTransactions.AddTransferContractAsync(newContract, cancellationToken);
            trade.TransferContract = newContract;
        }

        // Idempotency Check: Nếu vai trò này đã được ghi nhận chữ ký trước đó (ví dụ Webhook vừa xử lý xong trước Polling 50ms)
        var existingSig = trade.TransferContract.DigitalSignatures.FirstOrDefault(s => s.SignatureRole == role);
        if (existingSig != null)
        {
            var alreadyFullySigned = trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.BUYER)
                                  && trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.SELLER);

            return new SignContractResultDto(
                trade.Id,
                trade.TradeTransactionCode,
                role.ToString(),
                existingSig.SignedAt,
                alreadyFullySigned,
                trade.TransferContract.ContractStatus.ToString(),
                "Chữ ký số VNPT SmartCA của bạn đã được ghi nhận thành công.");
        }

        string? signatureValueBase64 = request.WebhookSignatureValueBase64;
        string? docId = request.WebhookDocId ?? session?.DocId ?? $"HD-{trade.TradeTransactionCode}-{role}";

        if (string.IsNullOrWhiteSpace(signatureValueBase64))
        {
            var statusResult = await _smartCaService.GetSignTransactionStatusAsync(
                request.TranCodeOrTransactionId,
                cancellationToken);

            var firstSig = statusResult.Signatures.FirstOrDefault();
            if (!statusResult.IsCompleted || string.IsNullOrWhiteSpace(firstSig?.SignatureValueBase64))
            {
                throw new BadRequestException(
                    "Đang chờ người dùng xác nhận ký số trên ứng dụng di động VNPT SmartCA (sig_wait_for_user_confirm).");
            }

            signatureValueBase64 = firstSig.SignatureValueBase64;
            if (!string.IsNullOrWhiteSpace(firstSig.DocId))
            {
                docId = firstSig.DocId;
            }
        }

        var serialNumber = !string.IsNullOrWhiteSpace(request.SerialNumber)
            ? request.SerialNumber
            : (session?.SerialNumber ?? "VNPT_SMARTCA_CERT");

        var now = DateTime.UtcNow;
        var sigHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatureValueBase64))).ToLowerInvariant();

        var auditJson = JsonSerializer.Serialize(new
        {
            Provider = "VNPT_SMARTCA",
            TradeCode = trade.TradeTransactionCode,
            UserId = userId,
            Role = role.ToString(),
            TranCode = request.TranCodeOrTransactionId,
            DocId = docId,
            SerialNumber = serialNumber,
            Timestamp = now
        });

        var signature = new DigitalSignature
        {
            Id = Guid.NewGuid(),
            TransferContractId = trade.TransferContract.Id,
            UserId = userId,
            SignatureRole = role,
            AuthMethod = "VNPT_SMARTCA",
            IpAddress = request.IpAddress ?? session?.IpAddress ?? "unknown",
            UserAgent = request.UserAgent ?? session?.UserAgent ?? "VNPT_SmartCA_Gateway",
            OtpCodeHash = sigHash,
            OtpVerifiedAt = now,
            SignedAt = now,
            AuditMetadataJson = auditJson,
            PublicKey = $"SMARTCA_SERIAL:{serialNumber}",
            CryptographicSignature = signatureValueBase64
        };

        await _unitOfWork.TradeTransactions.AddDigitalSignatureAsync(signature, cancellationToken);
        trade.TransferContract.DigitalSignatures.Add(signature);

        var hasBuyer = trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.BUYER);
        var hasSeller = trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.SELLER);
        var isFullySigned = hasBuyer && hasSeller;

        if (isFullySigned)
        {
            _logger.LogInformation(
                "[SmartCA Sign] ✅ Cả Bên Mua và Bên Bán đã hoàn tất ký số giao dịch {TradeCode}. Chuyển FULLY_SIGNED và đẩy task niêm phong PAdES!",
                trade.TradeTransactionCode);

            trade.TransferContract.ContractStatus = ContractStatus.FULLY_SIGNED;
            trade.TradeTransactionStatus = TradeTransactionStatus.PendingTransfer;

            var transferDeadline = now.AddHours(_deadlineSettings.TransferDeadlineHours);
            trade.TransferDeadline = transferDeadline;

            if (trade.SimTransfer == null)
            {
                var simTransfer = new SimTransfer
                {
                    Id = Guid.NewGuid(),
                    TradeTransactionId = trade.Id,
                    StartAt = now,
                    Deadline = transferDeadline,
                    SimTransferStatus = SimTransferStatus.PENDING_SELLER_ACTION
                };
                await _unitOfWork.TradeTransactions.AddSimTransferAsync(simTransfer, cancellationToken);
                trade.SimTransfer = simTransfer;
            }
            else
            {
                trade.SimTransfer.Deadline = transferDeadline;
            }

            await _delayQueue.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.SIGNING_DEADLINE, cancellationToken);
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = trade.AuctionId,
                JobType = AuctionDelayJobTypes.TRANSFER_DEADLINE,
                ExecuteAt = transferDeadline,
                ExtraData = trade.Id.ToString()
            }, cancellationToken);

            await _signingQueue.EnqueueContractSigningTaskAsync(trade.Id, isAutoEnforced: false, cancellationToken);
        }
        else
        {
            trade.TransferContract.ContractStatus = ContractStatus.PARTIALLY_SIGNED;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var message = isFullySigned
            ? "Cả 2 bên đã hoàn tất ký số VNPT SmartCA. Hợp đồng đang được hệ thống niêm phong chữ ký số PAdES X.509."
            : $"Bạn ({role}) đã ký số bằng VNPT SmartCA thành công. Đang chờ bên còn lại hoàn tất ký hợp đồng.";

        return new SignContractResultDto(
            trade.Id,
            trade.TradeTransactionCode,
            role.ToString(),
            now,
            isFullySigned,
            trade.TransferContract.ContractStatus.ToString(),
            message);
    }
}
