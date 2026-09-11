using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Xử lý truy vấn chi tiết một giao dịch ví.
/// </summary>
public class GetWalletTransactionByIdQueryHandler : IRequestHandler<GetWalletTransactionByIdQuery, WalletTransactionDto>
{
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IWalletRepository _walletRepository;

    public GetWalletTransactionByIdQueryHandler(
        IWalletTransactionRepository walletTransactionRepository,
        IWalletRepository walletRepository)
    {
        _walletTransactionRepository = walletTransactionRepository;
        _walletRepository = walletRepository;
    }

    public async Task<WalletTransactionDto> Handle(GetWalletTransactionByIdQuery request, CancellationToken cancellationToken)
    {
        var transaction = await _walletTransactionRepository.GetByIdAsync(request.TransactionId, cancellationToken);
        if (transaction == null)
        {
            throw new NotFoundException($"Không tìm thấy giao dịch với ID '{request.TransactionId}'.");
        }

        var wallet = await _walletRepository.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null || wallet.UserAccountId != request.UserId)
        {
            throw new ForbiddenAccessException("Bạn không có quyền xem chi tiết giao dịch này.");
        }

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
            CompletedAt = transaction.CompletedAt,
            ExpiredAt = transaction.ExpiredAt
        };
    }
}
