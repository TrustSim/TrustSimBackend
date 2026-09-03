using AutoMapper;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateWithdraw;

/// <summary>
/// Xử lý yêu cầu rút tiền: kiểm tra số dư khả dụng, trừ tiền ví ngay lập tức và tạo giao dịch PENDING chờ Admin duyệt/chi tiền.
/// </summary>
public class CreateWithdrawCommandHandler : IRequestHandler<CreateWithdrawCommand, ApiResponse<WalletTransactionDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public CreateWithdrawCommandHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository walletTransactionRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _walletRepository = walletRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    public async Task<ApiResponse<WalletTransactionDto>> Handle(CreateWithdrawCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm ví người dùng
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Không thể rút tiền.");
        }

        // 2. Kiểm tra số dư khả dụng
        if (wallet.AvailableBalance < request.Amount)
        {
            throw new BadRequestException(
                $"Số dư khả dụng ({wallet.AvailableBalance:N0} VNĐ) không đủ để rút {request.Amount:N0} VNĐ.");
        }

        // 3. Trừ số dư khả dụng và cập nhật Audit trail
        var balanceBefore = wallet.AvailableBalance;
        wallet.AvailableBalance -= request.Amount;
        var balanceAfter = wallet.AvailableBalance;
        wallet.LockVersion++;

        // 4. Sinh mã giao dịch rút tiền
        var transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        while (await _walletTransactionRepository.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = $"WD{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
        }

        // 5. Che số tài khoản an toàn (Masked)
        var accNumber = request.DestinationAccountNumber.Trim();
        var maskedAccount = accNumber.Length > 4
            ? new string('*', accNumber.Length - 4) + accNumber[^4..]
            : new string('*', accNumber.Length);

        // 6. Tạo WalletTransaction với trạng thái PENDING
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransactionStatus = TransactionStatus.PENDING,
            WalletTransactionType = WalletTransactionType.WITHDRAW,
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            DestinationBankCode = request.DestinationBankCode.Trim(),
            DestinationBankName = request.DestinationBankName.Trim(),
            DestinationAccountNumber = accNumber,
            DestinationAccountNumberMasked = maskedAccount,
            DestinationAccountHolderName = request.DestinationAccountHolderName.Trim().ToUpperInvariant(),
            Description = request.Note ?? $"Rút tiền về {request.DestinationBankName} ({maskedAccount})",
            CreatedAt = DateTime.UtcNow
        };

        _walletRepository.Update(wallet);
        await _walletTransactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<WalletTransactionDto>(transaction);
        return ApiResponse<WalletTransactionDto>.SuccessResult(dto, "Yêu cầu rút tiền đã được gửi thành công và đang chờ xử lý.");
    }
}
