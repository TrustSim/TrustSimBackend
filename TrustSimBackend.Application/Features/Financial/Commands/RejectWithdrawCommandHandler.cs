using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
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

    public RejectWithdrawCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        // Hoàn tiền lại vào ví người dùng
        wallet.AvailableBalance += transaction.Amount;
        wallet.LockVersion++;

        transaction.TransactionStatus = TransactionStatus.CANCELLED;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.Description += $" [Admin từ chối: {request.Reason}]";

        _unitOfWork.Wallets.Update(wallet);
        _unitOfWork.WalletTransactions.Update(transaction);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
