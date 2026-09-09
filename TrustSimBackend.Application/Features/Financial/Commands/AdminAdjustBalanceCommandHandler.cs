using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

public class AdminAdjustBalanceCommandHandler : IRequestHandler<AdminAdjustBalanceCommand, bool>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminAdjustBalanceCommandHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AdminAdjustBalanceCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null) throw new NotFoundException($"Không tìm thấy ví tiền với ID '{request.WalletId}'.");

        var beforeBalance = wallet.AvailableBalance;
        var newBalance = wallet.AvailableBalance + request.Amount;
        if (newBalance < 0) throw new BadRequestException("Số dư khả dụng sau điều chỉnh không được nhỏ hơn 0.");

        wallet.AvailableBalance = newBalance;
        wallet.UpdatedDate = DateTime.UtcNow;
        _walletRepository.Update(wallet);

        var tx = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpper()}",
            WalletTransactionType = request.Amount >= 0 ? WalletTransactionType.TOP_UP : WalletTransactionType.WITHDRAW,
            Amount = Math.Abs(request.Amount),
            BalanceBefore = beforeBalance,
            BalanceAfter = newBalance,
            TransactionStatus = TransactionStatus.COMPLETED,
            Description = $"[Admin Adjustment] {request.Reason}",
            CreatedDate = DateTime.UtcNow
        };

        await _transactionRepository.AddAsync(tx, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
