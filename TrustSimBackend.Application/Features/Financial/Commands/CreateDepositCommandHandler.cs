using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Financial;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý tạo phiên giao dịch nạp tiền, sinh mã cú pháp thanh toán và URL mã VietQR SePay.
/// </summary>
public class CreateDepositCommandHandler : IRequestHandler<CreateDepositCommand, DepositResponseDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISePayService _sePayService;

    public CreateDepositCommandHandler(
        IUnitOfWork unitOfWork,
        ISePayService sePayService)
    {
        _unitOfWork = unitOfWork;
        _sePayService = sePayService;
    }

    public async Task<DepositResponseDto> Handle(CreateDepositCommand request, CancellationToken cancellationToken)
    {
        // 1. Tìm ví của người dùng
        var wallet = await _unitOfWork.Wallets.GetByUserIdAsync(request.UserId, cancellationToken);
        if (wallet == null)
        {
            throw new NotFoundException($"Không tìm thấy ví tiền của người dùng với ID '{request.UserId}'.");
        }

        if (wallet.WalletStatus != WalletStatus.ACTIVE)
        {
            throw new BadRequestException("Ví tiền của bạn đang bị khóa hoặc đóng băng. Vui lòng liên hệ bộ phận hỗ trợ.");
        }

        // 2. Sinh mã giao dịch duy nhất
        var transactionCode = _sePayService.GenerateTransactionCode();
        while (await _unitOfWork.WalletTransactions.ExistsByTransactionCodeAsync(transactionCode, cancellationToken))
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

        await _unitOfWork.WalletTransactions.AddAsync(transaction, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 5. Trả về kết quả DTO
        return new DepositResponseDto
        {
            TransactionId = transaction.Id,
            TransactionCode = transactionCode,
            Amount = request.Amount,
            TransferContent = transactionCode,
            QrCodeUrl = qrUrl,
            ExpiredAt = expiredAt
        };
    }
}
