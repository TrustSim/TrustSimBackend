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
/// Handler xử lý ProcessWithdrawFailedCommand (Pha 2B: Payout thất bại -> Tự hoàn tiền Nợ 2020 / Có 2010 -> CANCELLED).
/// </summary>
public class ProcessWithdrawFailedCommandHandler : IRequestHandler<ProcessWithdrawFailedCommand, WalletTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ProcessWithdrawFailedCommandHandler> _logger;

    public ProcessWithdrawFailedCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ProcessWithdrawFailedCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<WalletTransactionDto> Handle(ProcessWithdrawFailedCommand request, CancellationToken cancellationToken)
    {
        _logger.LogWarning("[Withdraw Phase 2B] Xử lý Payout thất bại cho GD='{Code}', Lý do='{Reason}'",
            request.TransactionCode, request.FailureReason);

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

        var reason = string.IsNullOrWhiteSpace(request.FailureReason)
            ? "Ngân hàng từ chối chi hộ"
            : request.FailureReason;

        // Ghi nhận sổ cái kép Pha 2B (Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE)
        var journal = await _ledgerService.PostWithdrawRefundAsync(
            wallet.Id,
            transaction.Amount,
            request.TransactionCode,
            reason,
            cancellationToken);

        transaction.TransactionStatus = TransactionStatus.CANCELLED;
        transaction.Description += $" | Payout thất bại: {reason} (Đã hoàn tiền - Chứng từ #{journal.JournalCode})";

        var paymentIntent = await _unitOfWork.PaymentIntents.GetByCodeAsync($"PI-{request.TransactionCode}", cancellationToken);
        if (paymentIntent != null)
        {
            paymentIntent.Status = PaymentIntentStatus.FAILED;
            paymentIntent.FailedReason = reason;
            _unitOfWork.PaymentIntents.Update(paymentIntent);
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[Withdraw Phase 2B] Đã tự động hoàn tiền {Amount:N0} VNĐ về ví khả dụng cho '{WalletId}' (Chứng từ: {JournalCode})",
            transaction.Amount, wallet.Id, journal.JournalCode);

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
