using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý Admin duyệt yêu cầu rút tiền.
/// </summary>
public class ApproveWithdrawCommandHandler : IRequestHandler<ApproveWithdrawCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;

    public ApproveWithdrawCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
    }

    public async Task<bool> Handle(ApproveWithdrawCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _unitOfWork.WalletTransactions.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch rút tiền với ID '{request.TransactionId}'.");
        }

        if (transaction.WalletTransactionType != WalletTransactionType.WITHDRAW)
        {
            throw new BadRequestException("Giao dịch này không phải là giao dịch rút tiền.");
        }

        if (transaction.TransactionStatus != TransactionStatus.PENDING)
        {
            throw new BadRequestException($"Giao dịch đã ở trạng thái '{transaction.TransactionStatus}', không thể duyệt lại.");
        }

        var wallet = transaction.Wallet ?? await _unitOfWork.Wallets.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền liên kết với giao dịch '{transaction.TransactionCode}'.");
        }

        var externalRef = $"ADMIN-APPR-{DateTime.UtcNow:yyMMddHHmmss}";

        // Ghi nhận sổ cái kép Pha 2A (Nợ 2020 USER_LOCKED / Có 1010 EXTERNAL_GATEWAY_CLEARING)
        var journal = await _ledgerService.PostWithdrawCompleteAsync(
            wallet.Id,
            transaction.Amount,
            transaction.TransactionCode,
            externalRef,
            cancellationToken);

        transaction.TransactionStatus = TransactionStatus.COMPLETED;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.ExternalTransactionId = externalRef;
        transaction.BalanceAfter = wallet.AvailableBalance;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            transaction.Description += $" [Admin duyệt: {request.Note} - Chứng từ #{journal.JournalCode}]";
        }
        else
        {
            transaction.Description += $" | Admin duyệt hoàn tất (Chứng từ #{journal.JournalCode})";
        }

        var paymentIntent = await _unitOfWork.PaymentIntents.GetByCodeAsync($"PI-{transaction.TransactionCode}", cancellationToken);
        if (paymentIntent != null)
        {
            paymentIntent.Status = PaymentIntentStatus.SUCCEEDED;
            paymentIntent.SucceededAt = DateTime.UtcNow;
            paymentIntent.ExternalTransactionId = externalRef;
            _unitOfWork.PaymentIntents.Update(paymentIntent);
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
