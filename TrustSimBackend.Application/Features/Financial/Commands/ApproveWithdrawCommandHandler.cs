using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
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

    public ApproveWithdrawCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
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

        transaction.TransactionStatus = TransactionStatus.COMPLETED;
        transaction.CompletedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            transaction.Description += $" [Admin Note: {request.Note}]";
        }

        _unitOfWork.WalletTransactions.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
