using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands;

/// <summary>
/// Xử lý Webhook tự động từ SePay: xác thực Token, đối soát mã giao dịch, kiểm tra Idempotency và cộng tiền vào ví an toàn.
/// </summary>
public class ProcessSepayWebhookCommandHandler : IRequestHandler<ProcessSepayWebhookCommand, bool>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISePayService _sePayService;
    private readonly ILogger<ProcessSepayWebhookCommandHandler> _logger;

    public ProcessSepayWebhookCommandHandler(
        IWalletRepository walletRepository,
        IWalletTransactionRepository walletTransactionRepository,
        IUnitOfWork unitOfWork,
        ISePayService sePayService,
        ILogger<ProcessSepayWebhookCommandHandler> logger)
    {
        _walletRepository = walletRepository;
        _walletTransactionRepository = walletTransactionRepository;
        _unitOfWork = unitOfWork;
        _sePayService = sePayService;
        _logger = logger;
    }

    public async Task<bool> Handle(ProcessSepayWebhookCommand request, CancellationToken cancellationToken)
    {
        var payload = request.WebhookPayload;

        _logger.LogInformation(
            "[SePay Webhook] Nhận Webhook giao dịch ID={Id}, Ngân hàng={Gateway}, Số tiền={Amount}, Nội dung='{Content}', Code='{Code}'",
            payload.Id, payload.Gateway, payload.TransferAmount, payload.Content, payload.Code);

        // 1. Xác thực Webhook Token / API Key
        if (!_sePayService.ValidateWebhookToken(request.AuthorizationHeader))
        {
            _logger.LogWarning("[SePay Webhook] Xác thực token thất bại.");
            throw new UnauthorizedException("Mã xác thực Webhook SePay không hợp lệ.");
        }

        // 2. Chỉ xử lý tiền vào ("in")
        if (!string.IsNullOrWhiteSpace(payload.TransferType) && !string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[SePay Webhook] Bỏ qua giao dịch không phải tiền vào (TransferType={Type}).", payload.TransferType);
            return true;
        }

        // 3. Trích xuất mã giao dịch TS...
        var transactionCode = (!string.IsNullOrWhiteSpace(payload.Content) ? _sePayService.ExtractTransactionCode(payload.Content) : null)
            ?? (!string.IsNullOrWhiteSpace(payload.Code) ? _sePayService.ExtractTransactionCode(payload.Code) ?? payload.Code.Trim() : null);

        if (string.IsNullOrWhiteSpace(transactionCode))
        {
            _logger.LogWarning("[SePay Webhook] Không tìm thấy mã giao dịch hợp lệ trong nội dung '{Content}' hoặc Code '{Code}'.", payload.Content, payload.Code);
            return true;
        }

        // 4. Tìm giao dịch trong Database (hỗ trợ cả trích xuất lẫn Code nguyên bản)
        var transaction = await _walletTransactionRepository.GetByTransactionCodeAsync(transactionCode, cancellationToken);
        if (transaction == null && !string.IsNullOrWhiteSpace(payload.Code))
        {
            transaction = await _walletTransactionRepository.GetByTransactionCodeAsync(payload.Code.Trim(), cancellationToken);
        }

        if (transaction == null)
        {
            _logger.LogWarning("[SePay Webhook] Không tìm thấy WalletTransaction với mã '{Code}' trong cơ sở dữ liệu.", transactionCode);
            return true;
        }

        // 5. Kiểm tra tính Idempotent
        if (transaction.TransactionStatus == TransactionStatus.COMPLETED)
        {
            _logger.LogInformation("[SePay Webhook] Giao dịch '{Code}' đã hoàn tất trước đó. Bỏ qua.", transactionCode);
            return true;
        }

        // 6. Kiểm tra số tiền nhận
        if (payload.TransferAmount <= 0)
        {
            _logger.LogWarning("[SePay Webhook] Số tiền nhận không hợp lệ ({Amount} VNĐ) cho mã '{Code}'.", payload.TransferAmount, transactionCode);
            return false;
        }

        // 7. Lấy ví người dùng và cộng tiền theo số tiền thực nhận
        var wallet = transaction.Wallet ?? await _walletRepository.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            _logger.LogError("[SePay Webhook] Không tìm thấy Wallet với ID '{WalletId}'.", transaction.WalletId);
            throw new NotFoundException($"Không tìm thấy ví tiền với ID '{transaction.WalletId}'.");
        }

        var amountToCredit = payload.TransferAmount;
        transaction.Amount = amountToCredit;
        transaction.BalanceBefore = wallet.AvailableBalance;
        wallet.AvailableBalance += amountToCredit;
        wallet.LockVersion++;
        transaction.BalanceAfter = wallet.AvailableBalance;

        transaction.TransactionStatus = TransactionStatus.COMPLETED;
        transaction.CompletedAt = DateTime.UtcNow;
        transaction.ExternalTransactionId = payload.Id.ToString();
        transaction.Description = $"Nạp tiền thành công qua SePay ({payload.Gateway} - GD #{payload.Id})";

        _walletRepository.Update(wallet);
        _walletTransactionRepository.Update(transaction);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "[SePay Webhook] Nạp tiền THÀNH CÔNG cho ví '{WalletId}'. Số tiền: {Amount:N0} VNĐ. Số dư mới: {NewBalance:N0} VNĐ.",
            wallet.Id, amountToCredit, wallet.AvailableBalance);

        return true;
    }
}
