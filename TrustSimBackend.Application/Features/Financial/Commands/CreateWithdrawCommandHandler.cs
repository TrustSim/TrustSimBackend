using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý hành động rút tiền cá nhân của người dùng.
/// </summary>
public class CreateWithdrawCommandHandler : IRequestHandler<CreateWithdrawCommand, WalletTransactionDto>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateWithdrawCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<WalletTransactionDto> Handle(CreateWithdrawCommand request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0)
        {
            throw new BadRequestException("Số tiền rút phải lớn hơn 0 VNĐ.");
        }

        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
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

        var balanceBefore = wallet.AvailableBalance;
        wallet.AvailableBalance -= request.Amount;
        var balanceAfter = wallet.AvailableBalance;
        wallet.LockVersion++;

        var transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        while (await _unitOfWork.WalletTransactions.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        }

        var accNumber = request.DestinationAccountNumber.Trim();
        var maskedAccount = accNumber.Length > 4
            ? new string('*', accNumber.Length - 4) + accNumber[^4..]
            : new string('*', accNumber.Length);

        var now = DateTime.UtcNow;
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransactionStatus = TransactionStatus.COMPLETED,
            WalletTransactionType = WalletTransactionType.WITHDRAW,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            DestinationBankCode = request.DestinationBankCode.Trim(),
            DestinationBankName = request.DestinationBankName.Trim(),
            DestinationAccountNumber = accNumber,
            DestinationAccountNumberMasked = maskedAccount,
            DestinationAccountHolderName = request.DestinationAccountHolderName.Trim().ToUpperInvariant(),
            Description = request.Note ?? $"Rút tiền thành công về {request.DestinationBankName} ({maskedAccount})",
            CreatedAt = now,
            CompletedAt = now
        };

        _unitOfWork.Wallets.Update(wallet);
        await _unitOfWork.WalletTransactions.AddAsync(transaction, cancellationToken);
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
