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
/// Xử lý yêu cầu rút tiền thực tế của người dùng (Tự động hoàn tất 2 Pha nguyên tử: Lock -> Mock Bank Payout -> Complete).
/// Luôn tạo đủ 2 Chứng từ Sổ cái Kép (WITHDRAW_LOCK và WITHDRAW_COMPLETED).
/// </summary>
public class CreateWithdrawCommandHandler : IRequestHandler<CreateWithdrawCommand, WalletTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly IMockBankPayoutGatewayService _mockBankPayoutGatewayService;

    public CreateWithdrawCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        IMockBankPayoutGatewayService mockBankPayoutGatewayService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _mockBankPayoutGatewayService = mockBankPayoutGatewayService;
    }

    public async Task<WalletTransactionDto> Handle(CreateWithdrawCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount < 50_000)
        {
            throw new BadRequestException("Số tiền rút tối thiểu là 50.000 VNĐ.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Không thể rút tiền.");
        }

        if (wallet.AvailableBalance < request.Amount)
        {
            throw new BadRequestException(
                $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để rút {request.Amount:N0} VNĐ.");
        }

        var transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        while (await _unitOfWork.WalletTransactions.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        }

        var accNumber = request.DestinationAccountNumber.Trim();
        var maskedAccount = accNumber.Length > 4
            ? new string('*', accNumber.Length - 4) + accNumber[^4..]
            : new string('*', accNumber.Length);

        var balanceBefore = wallet.AvailableBalance;

        // [PHA 1]: Ghi nhận sổ cái kép Pha 1 (Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED)
        var lockJournal = await _ledgerService.PostWithdrawLockAsync(
            wallet.Id,
            request.Amount,
            transactionCode,
            idempotencyKey: $"WD-LOCK-{transactionCode}",
            cancellationToken: cancellationToken);

        // [MOCK BANK PAYOUT]: Gọi dịch vụ chi hộ ngân hàng Sandbox (luôn thành công)
        var externalPayoutRef = await _mockBankPayoutGatewayService.RequestInstantPayoutAsync(
            request.DestinationBankCode.Trim(),
            accNumber,
            request.DestinationAccountHolderName.Trim(),
            request.Amount,
            transactionCode,
            cancellationToken);

        // [PHA 2]: Ghi nhận sổ cái kép Pha 2 (Nợ 2020 USER_LOCKED / Có 1010 EXTERNAL_GATEWAY_CLEARING)
        var completeJournal = await _ledgerService.PostWithdrawCompleteAsync(
            wallet.Id,
            request.Amount,
            transactionCode,
            externalPayoutRef,
            cancellationToken);

        var now = DateTime.UtcNow;

        // 3. Tạo bản ghi WalletTransaction với trạng thái COMPLETED
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransactionStatus = TransactionStatus.COMPLETED,
            WalletTransactionType = WalletTransactionType.WITHDRAW,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.AvailableBalance,
            DestinationBankCode = request.DestinationBankCode.Trim(),
            DestinationBankName = request.DestinationBankName.Trim(),
            DestinationAccountNumber = accNumber,
            DestinationAccountNumberMasked = maskedAccount,
            DestinationAccountHolderName = request.DestinationAccountHolderName.Trim().ToUpperInvariant(),
            ExternalTransactionId = externalPayoutRef,
            Description = request.Note ?? $"Rút tiền về {request.DestinationBankName} ({maskedAccount}) - Chứng từ #{lockJournal.JournalCode} & #{completeJournal.JournalCode}",
            CreatedAt = now,
            CompletedAt = now
        };

        // 4. Tạo PaymentIntent với trạng thái SUCCEEDED
        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            IntentCode = $"PI-{transactionCode}",
            UserId = request.UserId,
            WalletId = wallet.Id,
            IntentType = PaymentIntentType.WITHDRAW,
            Status = PaymentIntentStatus.SUCCEEDED,
            Amount = request.Amount,
            Currency = "VND",
            IdempotencyKey = $"WD-INTENT-{transactionCode}",
            GatewayProvider = "MockBankPayout",
            ExternalTransactionId = externalPayoutRef,
            PaymentDetails = $"Rút về {request.DestinationBankName} - STK: {accNumber} - Chủ TK: {request.DestinationAccountHolderName.Trim().ToUpperInvariant()}",
            CreatedDate = now,
            SucceededAt = now
        };

        await _unitOfWork.WalletTransactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.PaymentIntents.AddAsync(paymentIntent, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new WalletTransactionDto
        {
            Id = transaction.Id,
            WalletId = transaction.WalletId,
            TransactionCode = transaction.TransactionCode,
            Amount = transaction.Amount,
            TransactionStatus = transaction.TransactionStatus,
            WalletTransactionType = transaction.WalletTransactionType,
            BalanceBefore = transaction.BalanceBefore,
            BalanceAfter = transaction.BalanceAfter,
            DestinationBankCode = transaction.DestinationBankCode,
            DestinationBankName = transaction.DestinationBankName,
            DestinationAccountNumberMasked = transaction.DestinationAccountNumberMasked,
            DestinationAccountHolderName = transaction.DestinationAccountHolderName,
            Description = transaction.Description,
            CreatedAt = transaction.CreatedAt,
            CompletedAt = transaction.CompletedAt
        };
    }
}
