using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý Admin từ chối yêu cầu rút tiền và hoàn tiền vào ví người dùng.
/// </summary>
public class RejectWithdrawCommandHandler : IRequestHandler<RejectWithdrawCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;

    public RejectWithdrawCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
    }

    public async Task<bool> Handle(RejectWithdrawCommand request, CancellationToken cancellationToken)
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
            throw new BadRequestException($"Giao dịch đã ở trạng thái '{transaction.TransactionStatus}', không thể từ chối.");
        }

        var wallet = await _unitOfWork.Wallets.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền với ID '{transaction.WalletId}'.");
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Admin từ chối yêu cầu rút tiền" : request.Reason;

        // Ghi nhận sổ cái kép Pha 2B (Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE)
        var journal = await _ledgerService.PostWithdrawRefundAsync(
            wallet.Id,
            transaction.Amount,
            transaction.TransactionCode,
            reason,
            cancellationToken);

        transaction.BalanceAfter = wallet.AvailableBalance;
        transaction.TransactionStatus = TransactionStatus.CANCELLED;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.Description += $" [Admin từ chối: {reason} - Chứng từ #{journal.JournalCode}]";

        var paymentIntent = await _unitOfWork.PaymentIntents.GetByCodeAsync($"PI-{transaction.TransactionCode}", cancellationToken);
        if (paymentIntent != null)
        {
            paymentIntent.Status = PaymentIntentStatus.FAILED;
            paymentIntent.FailedReason = reason;
            _unitOfWork.PaymentIntents.Update(paymentIntent);
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
