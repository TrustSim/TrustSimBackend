using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Command xử lý Webhook Callback từ Cổng chi hộ ngân hàng đối với yêu cầu rút tiền (Pha 2).
/// </summary>
public record ProcessWithdrawCallbackCommand(ProcessWithdrawCallbackRequest CallbackData) : IRequest<bool>;

public class ProcessWithdrawCallbackCommandHandler : IRequestHandler<ProcessWithdrawCallbackCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILedgerService _ledgerService;
    private readonly ILogger<ProcessWithdrawCallbackCommandHandler> _logger;

    public ProcessWithdrawCallbackCommandHandler(
        IUnitOfWork unitOfWork,
        ILedgerService ledgerService,
        ILogger<ProcessWithdrawCallbackCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _ledgerService = ledgerService;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessWithdrawCallbackCommand request, CancellationToken cancellationToken)
    {
        var data = request.CallbackData;
        _logger.LogInformation(
            "[Bank Payout Callback] Nhận phản hồi cho GD='{Code}', IsSuccess={Success}, Ref='{Ref}', Lý do='{Reason}'",
            data.TransactionCode, data.IsSuccess, data.ExternalReference, data.FailureReason);

        var transaction = await _unitOfWork.WalletTransactions.GetByTransactionCodeAsync(data.TransactionCode, cancellationToken);
        if (transaction == null)
        {
            _logger.LogWarning("[Bank Payout Callback] Không tìm thấy giao dịch với mã '{Code}'.", data.TransactionCode);
            throw new NotFoundException($"Không tìm thấy giao dịch rút tiền với mã '{data.TransactionCode}'.");
        }

        if (transaction.TransactionStatus != TransactionStatus.PENDING)
        {
            _logger.LogInformation("[Bank Payout Callback] Giao dịch '{Code}' đã ở trạng thái {Status}. Bỏ qua.", data.TransactionCode, transaction.TransactionStatus);
            return true;
        }

        var wallet = transaction.Wallet ?? await _unitOfWork.Wallets.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền với ID '{transaction.WalletId}'.");
        }

        var paymentIntent = await _unitOfWork.PaymentIntents.GetByCodeAsync($"PI-{data.TransactionCode}", cancellationToken);

        if (data.IsSuccess)
        {
            // Pha 2A: Chi trả thành công -> Nợ 2020 USER_LOCKED / Có 1010 EXTERNAL_GATEWAY_CLEARING
            var journal = await _ledgerService.PostWithdrawCompleteAsync(
                wallet.Id,
                transaction.Amount,
                data.TransactionCode,
                data.ExternalReference,
                cancellationToken);

            transaction.TransactionStatus = TransactionStatus.COMPLETED;
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.ExternalTransactionId = data.ExternalReference;
            transaction.Description += $" | Payout thành công (Chứng từ #{journal.JournalCode})";

            if (paymentIntent != null)
            {
                paymentIntent.Status = PaymentIntentStatus.SUCCEEDED;
                paymentIntent.SucceededAt = DateTime.UtcNow;
                paymentIntent.ExternalTransactionId = data.ExternalReference;
                _unitOfWork.PaymentIntents.Update(paymentIntent);
            }

            _logger.LogInformation("[Bank Payout Callback] Rút tiền THÀNH CÔNG cho ví '{WalletId}', Số tiền: {Amount:N0} VNĐ.", wallet.Id, transaction.Amount);
        }
        else
        {
            // Pha 2B: Chi trả thất bại -> Tự động hoàn tiền: Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE
            var reason = data.FailureReason ?? "Ngân hàng từ chối chi hộ";
            var journal = await _ledgerService.PostWithdrawRefundAsync(
                wallet.Id,
                transaction.Amount,
                data.TransactionCode,
                reason,
                cancellationToken);

            transaction.TransactionStatus = TransactionStatus.CANCELLED;
            transaction.Description += $" | Payout thất bại: {reason} (Đã hoàn tiền - Chứng từ #{journal.JournalCode})";

            if (paymentIntent != null)
            {
                paymentIntent.Status = PaymentIntentStatus.FAILED;
                paymentIntent.FailedReason = reason;
                _unitOfWork.PaymentIntents.Update(paymentIntent);
            }

            _logger.LogWarning("[Bank Payout Callback] Rút tiền THẤT BẠI cho ví '{WalletId}'. Đã tự động hoàn tiền về ví khả dụng.", wallet.Id);
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
