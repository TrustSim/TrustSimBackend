using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

public class AdminFreezeWalletCommandHandler : IRequestHandler<AdminFreezeWalletCommand, bool>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AdminFreezeWalletCommandHandler(IWalletRepository walletRepository, IUnitOfWork unitOfWork)
    {
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AdminFreezeWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null) throw new NotFoundException($"Không tìm thấy ví tiền với ID '{request.WalletId}'.");

        wallet.WalletStatus = request.Freeze ? WalletStatus.FROZEN : WalletStatus.ACTIVE;
        wallet.UpdatedDate = DateTime.UtcNow;

        _walletRepository.Update(wallet);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
