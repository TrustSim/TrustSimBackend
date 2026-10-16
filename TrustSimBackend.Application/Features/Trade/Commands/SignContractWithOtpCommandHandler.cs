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
/// Handler xử lý ký xác nhận hợp đồng điện tử bằng mã OTP Email cho Bên Mua hoặc Bên Bán.
/// </summary>
public class SignContractWithOtpCommandHandler : IRequestHandler<SignContractWithOtpCommand, SignContractResultDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IContractSigningQueueService _signingQueue;
    private readonly IAuctionDelayQueueService _delayQueue;
    private readonly TradeDeadlineSettings _deadlineSettings;
    private readonly ILogger<SignContractWithOtpCommandHandler> _logger;

    public SignContractWithOtpCommandHandler(
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IContractSigningQueueService signingQueue,
        IAuctionDelayQueueService delayQueue,
        IOptions<TradeDeadlineSettings> deadlineSettingsOptions,
        ILogger<SignContractWithOtpCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _signingQueue = signingQueue;
        _delayQueue = delayQueue;
        _deadlineSettings = deadlineSettingsOptions.Value;
        _logger = logger;
    }

    public async Task<SignContractResultDto> Handle(SignContractWithOtpCommand request, CancellationToken cancellationToken)
    {
        var trade = await _unitOfWork.TradeTransactions.GetByIdAsync(request.TradeId, cancellationToken);
        if (trade == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch chuyển nhượng có ID '{request.TradeId}'.");
        }

        if (trade.TradeTransactionStatus != TradeTransactionStatus.PendingSignature)
        {
            throw new BadRequestException("Giao dịch không ở trạng thái chờ ký hợp đồng.");
        }

        var isBuyer = trade.BuyerId == request.UserId;
        var isSeller = trade.SellerId == request.UserId;

        if (!isBuyer && !isSeller)
        {
            throw new ForbiddenAccessException("Bạn không phải là Bên Mua hoặc Bên Bán của giao dịch này.");
        }

        var role = isBuyer ? SignatureRole.BUYER : SignatureRole.SELLER;
        var user = isBuyer ? trade.Buyer : trade.Seller;

        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            throw new BadRequestException("Không tìm thấy địa chỉ email hợp lệ của người dùng.");
        }

        // 1. Xác thực mã OTP qua Email (consumeOnSuccess: false để không xóa OTP nếu lưu DB gặp lỗi)
        var verifyResult = await _otpService.VerifyEmailOtpAsync(
            user.Email,
            request.OtpCode,
            OtpPurpose.ContractSigning.ToString(),
            consumeOnSuccess: false);

        if (!verifyResult.IsValid)
        {
            throw new BadRequestException(verifyResult.Message);
        }

        // 2. Đảm bảo TransferContract tồn tại
        if (trade.TransferContract == null)
        {
            var newContract = new TransferContract
            {
                Id = Guid.NewGuid(),
                TradeTransactionId = trade.Id,
                ContractStatus = ContractStatus.DRAFT,
                DocumentUrl = null,
                DocumentHash = null
            };
            await _unitOfWork.TradeTransactions.AddTransferContractAsync(newContract, cancellationToken);
            trade.TransferContract = newContract;
        }

        // 3. Kiểm tra xem vai trò này đã ký trước đó chưa
        if (trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == role))
        {
            throw new BadRequestException("Bạn đã ký xác nhận hợp đồng này rồi.");
        }

        // 4. Tính toán mã băm SHA-256 của mã OTP và tạo dấu vết kiểm toán (Audit Trail)
        var otpHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.OtpCode))).ToLower();
        var now = DateTime.UtcNow;

        var auditJson = JsonSerializer.Serialize(new
        {
            TradeCode = trade.TradeTransactionCode,
            UserId = request.UserId,
            Email = user.Email,
            Role = role.ToString(),
            Timestamp = now
        });

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(otpHash));
        var authSignature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(auditJson))).ToLowerInvariant();

        var signature = new DigitalSignature
        {
            Id = Guid.NewGuid(),
            TransferContractId = trade.TransferContract.Id,
            UserId = request.UserId,
            SignatureRole = role,
            AuthMethod = "EMAIL_OTP",
            IpAddress = request.IpAddress ?? "unknown",
            UserAgent = request.UserAgent ?? "unknown",
            OtpCodeHash = otpHash,
            OtpVerifiedAt = now,
            SignedAt = now,
            AuditMetadataJson = auditJson,
            PublicKey = $"EMAIL_VERIFIED_IDENTITY:{user.Email}",
            CryptographicSignature = authSignature
        };

        await _unitOfWork.TradeTransactions.AddDigitalSignatureAsync(signature, cancellationToken);
        trade.TransferContract.DigitalSignatures.Add(signature);

        // 5. Kiểm tra nếu CẢ HAI BÊN đã ký xong
        var hasBuyer = trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.BUYER);
        var hasSeller = trade.TransferContract.DigitalSignatures.Any(s => s.SignatureRole == SignatureRole.SELLER);
        var isFullySigned = hasBuyer && hasSeller;

        if (isFullySigned)
        {
            _logger.LogInformation(
                "[SignContract] ✅ Cả Bên Mua và Bên Bán đã ký xác nhận giao dịch {TradeCode}. Cập nhật FULLY_SIGNED, tạo tiến trình bàn giao SIM và đẩy task niêm phong PAdES!",
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

            // Hủy tác vụ hẹn giờ SIGNING_DEADLINE trong Redis ZSET
            await _delayQueue.CancelJobAsync(trade.AuctionId, AuctionDelayJobTypes.SIGNING_DEADLINE, cancellationToken);

            // Nạp tác vụ hẹn giờ TRANSFER_DEADLINE vào Redis ZSET
            await _delayQueue.EnqueueJobAsync(new AuctionDelayJob
            {
                AuctionId = trade.AuctionId,
                JobType = AuctionDelayJobTypes.TRANSFER_DEADLINE,
                ExecuteAt = transferDeadline,
                ExtraData = trade.Id.ToString()
            }, cancellationToken);

            // Đẩy task vào Redis Stream cho Background Worker niêm phong PAdES
            await _signingQueue.EnqueueContractSigningTaskAsync(trade.Id, isAutoEnforced: false, cancellationToken);
        }
        else
        {
            trade.TransferContract.ContractStatus = ContractStatus.PARTIALLY_SIGNED;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 6. Sau khi lưu DB thành công mới tiêu thụ/hủy mã OTP
        await _otpService.ConsumeEmailOtpAsync(user.Email, OtpPurpose.ContractSigning.ToString());

        var message = isFullySigned
            ? "Cả 2 bên đã hoàn tất ký xác nhận. Hợp đồng đang được hệ thống niêm phong chữ ký số PAdES X.509."
            : $"Bạn ({role}) đã ký xác nhận thành công. Đang chờ bên còn lại hoàn tất ký hợp đồng.";

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
