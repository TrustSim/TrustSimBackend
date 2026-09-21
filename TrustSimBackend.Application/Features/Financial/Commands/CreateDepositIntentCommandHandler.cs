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
/// Handler xử lý CreateDepositIntentCommand (Khởi tạo Payment Intent nạp tiền và sinh mã VietQR).
/// </summary>
public class CreateDepositIntentCommandHandler : IRequestHandler<CreateDepositIntentCommand, PaymentIntentDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISePayService _sePayService;

    public CreateDepositIntentCommandHandler(
        IUnitOfWork unitOfWork,
        ISePayService sePayService)
    {
        _unitOfWork = unitOfWork;
        _sePayService = sePayService;
    }

    public async Task<PaymentIntentDto> Handle(CreateDepositIntentCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount < 10_000)
        {
            throw new BadRequestException("Số tiền nạp tối thiểu là 10.000 VNĐ.");
        }

        // 1. Kiểm tra ví người dùng
        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng.");
        }

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? Guid.NewGuid().ToString("N")
            : request.IdempotencyKey.Trim();

        // 2. Kiểm tra Idempotency
        var existingIntent = await _unitOfWork.PaymentIntents.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (existingIntent != null)
        {
            return PaymentIntentDto.FromEntity(existingIntent);
        }

        // 3. Sinh mã giao dịch duy nhất
        var transactionCode = _sePayService.GenerateTransactionCode();
        while (await _unitOfWork.WalletTransactions.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = _sePayService.GenerateTransactionCode();
        }

        var qrUrl = _sePayService.GeneratePaymentQrUrl(request.Amount, transactionCode);
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        // 4. Tạo PaymentIntent
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
            ExpiresAt = expiresAt,
            CreatedDate = DateTime.UtcNow
        };

        // 5. Tạo WalletTransaction tương ứng (PENDING)
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            QrCodeUrl = qrUrl,
            Description = $"Nạp tiền vào ví TrustSim - {transactionCode}",
            TransactionStatus = TransactionStatus.PENDING,
            WalletTransactionType = WalletTransactionType.TOP_UP,
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = expiresAt,
            BalanceBefore = wallet.AvailableBalance,
            BalanceAfter = wallet.AvailableBalance
        };

        await _unitOfWork.PaymentIntents.AddAsync(intent, cancellationToken);
        await _unitOfWork.WalletTransactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return PaymentIntentDto.FromEntity(intent);
    }
}
