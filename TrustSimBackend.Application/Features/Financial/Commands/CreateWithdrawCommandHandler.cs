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
/// Xử lý yêu cầu rút tiền của người dùng (Pha 1: Khóa số dư qua Sổ cái kép &amp; chờ Payout ngân hàng).
/// </summary>
public class CreateWithdrawCommandHandler : IRequestHandler<CreateWithdrawCommand, WalletTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;

    public CreateWithdrawCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
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

        // 1. Ghi nhận sổ cái kép Pha 1 (Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED)
        var journal = await _ledgerService.PostWithdrawLockAsync(
            wallet.Id,
            request.Amount,
            transactionCode,
            idempotencyKey: $"WD-LOCK-{transactionCode}",
            cancellationToken: cancellationToken);

        var now = DateTime.UtcNow;

        // 2. Tạo bản ghi WalletTransaction (Status = PENDING)
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransactionStatus = TransactionStatus.PENDING,
            WalletTransactionType = WalletTransactionType.WITHDRAW,
            BalanceBefore = balanceBefore,
            BalanceAfter = wallet.AvailableBalance,
            DestinationBankCode = request.DestinationBankCode.Trim(),
            DestinationBankName = request.DestinationBankName.Trim(),
            DestinationAccountNumber = accNumber,
            DestinationAccountNumberMasked = maskedAccount,
            DestinationAccountHolderName = request.DestinationAccountHolderName.Trim().ToUpperInvariant(),
            Description = request.Note ?? $"Yêu cầu rút tiền về {request.DestinationBankName} ({maskedAccount}) - Chứng từ #{journal.JournalCode}",
            CreatedAt = now
        };

        // 3. Tạo PaymentIntent (Type = WITHDRAW, Status = PROCESSING)
        var paymentIntent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            IntentCode = $"PI-{transactionCode}",
            UserId = request.UserId,
            WalletId = wallet.Id,
            IntentType = PaymentIntentType.WITHDRAW,
            Status = PaymentIntentStatus.PROCESSING,
            Amount = request.Amount,
            Currency = "VND",
            IdempotencyKey = $"WD-INTENT-{transactionCode}",
            GatewayProvider = "BankPayout",
            PaymentDetails = $"Rút về {request.DestinationBankName} - STK: {accNumber} - Chủ TK: {request.DestinationAccountHolderName.Trim().ToUpperInvariant()}",
            CreatedDate = now
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
