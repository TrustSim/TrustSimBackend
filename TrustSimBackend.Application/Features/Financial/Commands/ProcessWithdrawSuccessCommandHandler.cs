using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Handler xử lý ProcessWithdrawSuccessCommand (Pha 2A: Chi trả thành công -> Nợ 2020 / Có 1010 -> COMPLETED).
/// </summary>
public class ProcessWithdrawSuccessCommandHandler : IRequestHandler<ProcessWithdrawSuccessCommand, WalletTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ProcessWithdrawSuccessCommandHandler> _logger;

    public ProcessWithdrawSuccessCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ProcessWithdrawSuccessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<WalletTransactionDto> Handle(ProcessWithdrawSuccessCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[Withdraw Phase 2A] Xử lý Payout thành công cho GD='{Code}', ExternalRef='{Ref}'",
            request.TransactionCode, request.ExternalBankRef);

        var transaction = await _unitOfWork.WalletTransactions.GetByTransactionCodeAsync(request.TransactionCode, cancellationToken);
        if (transaction == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch rút tiền với mã '{request.TransactionCode}'.");
        }

        if (transaction.TransactionStatus != TransactionStatus.PENDING)
        {
            throw new BadRequestException($"Giao dịch '{request.TransactionCode}' không ở trạng thái chờ (PENDING) mà đang là {transaction.TransactionStatus}.");
        }

        var wallet = transaction.Wallet ?? await _unitOfWork.Wallets.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền liên kết với giao dịch '{request.TransactionCode}'.");
        }

        var externalRef = request.ExternalBankRef ?? $"PAYOUT-MOCK-{Random.Shared.Next(100000, 999999)}";

        // Ghi nhận sổ cái kép Pha 2A (Nợ 2020 USER_LOCKED / Có 1010 EXTERNAL_GATEWAY_CLEARING)
        var journal = await _ledgerService.PostWithdrawCompleteAsync(
            wallet.Id,
            transaction.Amount,
            request.TransactionCode,
            externalRef,
            cancellationToken);

        var now = DateTime.UtcNow;
        transaction.TransactionStatus = TransactionStatus.COMPLETED;
        transaction.CompletedAt = now;
        transaction.ExternalTransactionId = externalRef;
        transaction.Description += $" | Payout thành công (Chứng từ #{journal.JournalCode})";

        var paymentIntent = await _unitOfWork.PaymentIntents.GetByCodeAsync($"PI-{request.TransactionCode}", cancellationToken);
        if (paymentIntent != null)
        {
            paymentIntent.Status = PaymentIntentStatus.SUCCEEDED;
            paymentIntent.SucceededAt = now;
            paymentIntent.ExternalTransactionId = externalRef;
            _unitOfWork.PaymentIntents.Update(paymentIntent);
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[Withdraw Phase 2A] Đã hoàn tất rút tiền cho ví '{WalletId}', Số tiền: {Amount:N0} VNĐ (Chứng từ: {JournalCode})",
            wallet.Id, transaction.Amount, journal.JournalCode);

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
