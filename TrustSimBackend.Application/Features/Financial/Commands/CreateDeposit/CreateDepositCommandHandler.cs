using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateDeposit;

/// <summary>
/// Xử lý tạo phiên giao dịch nạp tiền, sinh mã cú pháp thanh toán và URL mã VietQR SePay.
/// </summary>
public class CreateDepositCommandHandler : IRequestHandler<CreateDepositCommand, ApiResponse<DepositResponseDto>>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISePayService _sePayService;

    public CreateDepositCommandHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository walletTransactionRepository,
        IUnitOfWork unitOfWork,
        ISePayService sePayService)
    {
        _walletRepository = walletRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _unitOfWork = unitOfWork;
        _sePayService = sePayService;
    }

    public async Task<ApiResponse<DepositResponseDto>> Handle(CreateDepositCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm ví của người dùng
        var wallet = await _walletRepository.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Vui lòng liên hệ bộ phận hỗ trợ.");
        }

        // 2. Sinh mã giao dịch duy nhất (VD: TS982312)
        var transactionCode = _sePayService.GenerateTransactionCode();
        while (await _walletTransactionRepository.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
        {
            transactionCode = _sePayService.GenerateTransactionCode();
        }

        // 3. Sinh URL ảnh VietQR SePay
        var qrUrl = _sePayService.GeneratePaymentQrUrl(request.Amount, transactionCode);
        var expiredAt = DateTime.UtcNow.AddMinutes(30);

        // 4. Tạo bản ghi WalletTransaction (Status = PENDING)
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            WalletId = wallet.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            QrCodeUrl = qrUrl,
            Description = request.Note ?? $"Nạp tiền vào ví TrustSim - {transactionCode}",
            TransactionStatus = TransactionStatus.PENDING,
            WalletTransactionType = WalletTransactionType.TOP_UP,
            CreatedAt = DateTime.UtcNow,
            ExpiredAt = expiredAt,
            BalanceBefore = wallet.AvailableBalance,
            BalanceAfter = wallet.AvailableBalance
        };

        await _walletTransactionRepository.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Đóng gói kết quả trả về
        var responseDto = new DepositResponseDto
        {
            TransactionId = transaction.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransferContent = transactionCode,
            QrCodeUrl = qrUrl,
            ExpiredAt = expiredAt
        };

        return ApiResponse<DepositResponseDto>.SuccessResult(responseDto, "Tạo yêu cầu nạp tiền thành công. Vui lòng thanh toán theo thông tin hướng dẫn.");
    }
}
