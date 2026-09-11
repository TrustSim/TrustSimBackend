using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

/// <summary>
/// Xử lý truy vấn thông tin ví tiền của người dùng.
/// </summary>
public class GetMyWalletQueryHandler : IRequestHandler<GetMyWalletQuery, WalletDto>
{
    private readonly IWalletRepository _walletRepository;

    public GetMyWalletQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDto> Handle(GetMyWalletQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        return new WalletDto
        {
            Id = wallet.Id,
            UserAccountId = wallet.UserAccountId,
            AvailableBalance = wallet.AvailableBalance,
            LockedBalance = wallet.LockedBalance,
            WalletStatus = wallet.WalletStatus,
            LockVersion = wallet.LockVersion
        };
    }
}
