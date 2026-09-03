using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands.ApproveWithdraw;

/// <summary>
/// Xử lý Admin duyệt yêu cầu rút tiền: chuyển trạng thái giao dịch sang COMPLETED.
/// </summary>
public class ApproveWithdrawCommandHandler : IRequestHandler<ApproveWithdrawCommand, ApiResponse<bool>>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ApproveWithdrawCommandHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IUnitOfWork unitOfWork)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<bool>> Handle(ApproveWithdrawCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _walletTransactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
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

        _walletTransactionRepository.Update(transaction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<bool>.SuccessResult(true, "Duyệt yêu cầu rút tiền thành công.");
    }
}
