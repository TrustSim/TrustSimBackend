using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý tạo phiên giao dịch nạp tiền chuẩn Idempotent, tái sử dụng/cập nhật phiên PENDING duy nhất và sinh mã VietQR SePay.
/// </summary>
public class CreateDepositCommandHandler : IRequestHandler<CreateDepositCommand, DepositResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISePayService _sePayService;

    public CreateDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ISePayService sePayService)
    {
        _unitOfWork = unitOfWork;
        _sePayService = sePayService;
    }

    public async Task<DepositResponseDto> Handle(CreateDepositCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount < 10_000)
        {
            throw new BadRequestException("Số tiền nạp tối thiểu là 10.000 VNĐ.");
        }

        // 1. Tìm ví của người dùng
        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Vui lòng liên hệ bộ phận hỗ trợ.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim();

        // 2. Kiểm tra IdempotencyKey nếu được truyền lên từ Client
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existingIntent = await _unitOfWork.PaymentIntents.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
            if (existingIntent != null)
            {
                var existingTxCode = existingIntent.IntentCode.StartsWith("PI-")
                    ? existingIntent.IntentCode[3..]
                    : existingIntent.IntentCode;

                var existingTx = await _unitOfWork.WalletTransactions.GetByTransactionCodeAsync(existingTxCode, cancellationToken);
                if (existingTx != null)
                {
                    return new DepositResponseDto
                    {
                        TransactionId = existingTx.Id,
                        TransactionCode = existingTx.TransactionCode,
                        Amount = existingTx.Amount,
                        TransferContent = existingTx.TransactionCode,
                        QrCodeUrl = existingTx.QrCodeUrl ?? _sePayService.GeneratePaymentQrUrl(existingTx.Amount, existingTx.TransactionCode),
                        ExpiredAt = existingTx.ExpiredAt ?? DateTime.UtcNow.AddMinutes(30)
                    };
                }
            }
        }

        // 3. Kiểm tra xem ví có giao dịch TOP_UP ở trạng thái PENDING còn hiệu lực không
        var activePendingTx = await _unitOfWork.WalletTransactions.GetActivePendingTopUpByWalletIdAsync(wallet.Id, cancellationToken);
        if (activePendingTx != null)
        {
            // Trường hợp 3A: Cùng số tiền -> Tái sử dụng chính bản ghi PENDING này
            if (activePendingTx.Amount == request.Amount)
            {
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    var intentCode = $"PI-{activePendingTx.TransactionCode}";
                    var intent = await _unitOfWork.PaymentIntents.GetByCodeAsync(intentCode, cancellationToken);
                    if (intent == null)
                    {
                        intent = new PaymentIntent
                        {
                            Id = Guid.NewGuid(),
                            IntentCode = intentCode,
                            UserId = request.UserId,
                            WalletId = wallet.Id,
                            IntentType = PaymentIntentType.DEPOSIT,
                            Status = PaymentIntentStatus.CREATED,
                            Amount = request.Amount,
                            Currency = "VND",
                            IdempotencyKey = idempotencyKey,
                            GatewayProvider = "SePay",
                            QrCodeUrl = activePendingTx.QrCodeUrl,
                            PaymentDetails = $"Nạp tiền qua SePay QR - Cú pháp: {activePendingTx.TransactionCode}",
                            ExpiresAt = activePendingTx.ExpiredAt ?? DateTime.UtcNow.AddMinutes(30),
                            CreatedDate = activePendingTx.CreatedAt
                        };
                        await _unitOfWork.PaymentIntents.AddAsync(intent, cancellationToken);
                        await _unitOfWork.SaveChangesAsync(cancellationToken);
                    }
                }

                return new DepositResponseDto
                {
                    TransactionId = activePendingTx.Id,
                    TransactionCode = activePendingTx.TransactionCode,
                    Amount = activePendingTx.Amount,
                    TransferContent = activePendingTx.TransactionCode,
                    QrCodeUrl = activePendingTx.QrCodeUrl ?? _sePayService.GeneratePaymentQrUrl(activePendingTx.Amount, activePendingTx.TransactionCode),
                    ExpiredAt = activePendingTx.ExpiredAt ?? DateTime.UtcNow.AddMinutes(30)
                };
            }

            // Trường hợp 3B: Người dùng đổi số tiền nạp -> Cập nhật trực tiếp trên chính bản ghi PENDING đó
            var updatedQrUrl = _sePayService.GeneratePaymentQrUrl(request.Amount, activePendingTx.TransactionCode);
            var newExpiredAt = DateTime.UtcNow.AddMinutes(30);

            activePendingTx.Amount = request.Amount;
            activePendingTx.QrCodeUrl = updatedQrUrl;
            activePendingTx.ExpiredAt = newExpiredAt;
            activePendingTx.Description = request.Note ?? $"Nạp tiền vào ví TrustSim - {activePendingTx.TransactionCode}";

            _unitOfWork.WalletTransactions.Update(activePendingTx);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new DepositResponseDto
            {
                TransactionId = activePendingTx.Id,
                TransactionCode = activePendingTx.TransactionCode,
                Amount = request.Amount,
                TransferContent = activePendingTx.TransactionCode,
                QrCodeUrl = updatedQrUrl,
                ExpiredAt = newExpiredAt
            };
        }

        // 4. Nếu chưa có giao dịch PENDING nào -> Tạo mới 1 bản ghi duy nhất
        var transactionCode = _sePayService.GenerateTransactionCode();
        while (await _unitOfWork.WalletTransactions.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = _sePayService.GenerateTransactionCode();
        }

        var qrUrl = _sePayService.GeneratePaymentQrUrl(request.Amount, transactionCode);
        var expiredAt = DateTime.UtcNow.AddMinutes(30);

        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            QrCodeUrl = qrUrl,
            Description = request.Note ?? $"Nạp tiền vào ví TrustSim - {transactionCode}",
            TransactionStatus = TransactionStatus.PENDING,
            WalletTransactionType = WalletTransactionType.TOP_UP,
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = expiredAt,
            BalanceBefore = wallet.AvailableBalance,
            BalanceAfter = wallet.AvailableBalance
        };

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var intent = new PaymentIntent
            {
                Id = Guid.NewGuid(),
                IntentCode = $"PI-{transactionCode}",
                UserId = request.UserId,
                WalletId = wallet.Id,
                IntentType = PaymentIntentType.DEPOSIT,
                Status = PaymentIntentStatus.CREATED,
                Amount = request.Amount,
                Currency = "VND",
                IdempotencyKey = idempotencyKey,
                GatewayProvider = "SePay",
                QrCodeUrl = qrUrl,
                PaymentDetails = $"Nạp tiền qua SePay QR - Cú pháp: {transactionCode}",
                ExpiresAt = expiredAt,
                CreatedDate = DateTime.UtcNow
            };
            await _unitOfWork.PaymentIntents.AddAsync(intent, cancellationToken);
        }

        await _unitOfWork.WalletTransactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Trả về kết quả DTO
        return new DepositResponseDto
        {
            TransactionId = transaction.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransferContent = transactionCode,
            QrCodeUrl = qrUrl,
            ExpiredAt = expiredAt
        };
    }
}

