using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Commands.ProcessSepayWebhook;

/// <summary>
/// Xử lý Webhook tự động từ SePay: xác thực Token, đối soát mã giao dịch, kiểm tra Idempotency và cộng tiền vào ví an toàn.
/// </summary>
public class ProcessSepayWebhookCommandHandler : IRequestHandler<ProcessSepayWebhookCommand, ApiResponse<bool>>
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

    public async Task<ApiResponse<bool>> Handle(ProcessSepayWebhookCommand request, CancellationToken cancellationToken)
    {
        var payload = request.WebhookPayload;

        _logger.LogInformation(
            "[SePay Webhook] Nhận Webhook giao dịch ID={Id}, Ngân hàng={Gateway}, Số tiền={Amount}, Nội dung='{Content}'",
            payload.Id, payload.Gateway, payload.TransferAmount, payload.Content);

        // 1. Xác thực Webhook Token / API Key
        if (!_sePayService.ValidateWebhookToken(request.AuthorizationHeader))
        {
            _logger.LogWarning("[SePay Webhook] Xác thực token thất bại.");
            throw new UnauthorizedException("Mã xác thực Webhook SePay không hợp lệ.");
        }

        // 2. Chỉ xử lý tiền vào ("in")
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("[SePay Webhook] Bỏ qua giao dịch không phải tiền vào (TransferType={Type}).", payload.TransferType);
            return ApiResponse<bool>.SuccessResult(true, "Bỏ qua giao dịch không phải nạp tiền.");
        }

        // 3. Trích xuất mã giao dịch (từ trường Code hoặc nội dung Content)
        var transactionCode = !string.IsNullOrWhiteSpace(payload.Code)
            ? payload.Code.Trim()
            : _sePayService.ExtractTransactionCode(payload.Content);

        if (string.IsNullOrWhiteSpace(transactionCode))
        {
            _logger.LogWarning("[SePay Webhook] Không tìm thấy mã giao dịch hợp lệ trong nội dung '{Content}'.", payload.Content);
            return ApiResponse<bool>.SuccessResult(true, "Không nhận diện được mã giao dịch trong nội dung chuyển khoản.");
        }

        // 4. Tìm giao dịch trong Database
        var transaction = await _walletTransactionRepository.GetByTransactionCodeAsync(transactionCode, cancellationToken);
        if (transaction == null)
        {
            _logger.LogWarning("[SePay Webhook] Không tìm thấy WalletTransaction với mã '{Code}'.", transactionCode);
            return ApiResponse<bool>.SuccessResult(true, $"Không tìm thấy giao dịch với mã '{transactionCode}'.");
        }

        // 5. Kiểm tra tính Idempotent (Nếu đã COMPLETED thì bỏ qua ngay để không cộng tiền 2 lần)
        if (transaction.TransactionStatus == TransactionStatus.COMPLETED)
        {
            _logger.LogInformation("[SePay Webhook] Giao dịch '{Code}' đã hoàn tất trước đó. Bỏ qua để tránh xử lý trùng lặp.", transactionCode);
            return ApiResponse<bool>.SuccessResult(true, "Giao dịch đã được xử lý trước đó.");
        }

        // 6. Kiểm tra số tiền chuyển có khớp hoặc lớn hơn số tiền yêu cầu không
        if (payload.TransferAmount < transaction.Amount)
        {
            _logger.LogWarning(
                "[SePay Webhook] Số tiền thực nhận ({Actual} VNĐ) nhỏ hơn số tiền yêu cầu ({Expected} VNĐ) cho mã '{Code}'.",
                payload.TransferAmount, transaction.Amount, transactionCode);
            transaction.TransactionStatus = TransactionStatus.FAILED;
            transaction.Description += $" [Lỗi: Số tiền nhận {payload.TransferAmount:N0}đ < yêu cầu {transaction.Amount:N0}đ]";
            _walletTransactionRepository.Update(transaction);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return ApiResponse<bool>.SuccessResult(false, "Số tiền chuyển khoản không đủ theo yêu cầu.");
        }

        // 7. Lấy ví người dùng và cộng tiền
        var wallet = await _walletRepository.GetByIdAsync(transaction.WalletId, cancellationToken);
        if (wallet == null)
        {
            _logger.LogError("[SePay Webhook] Không tìm thấy Wallet với ID '{WalletId}' của giao dịch '{Code}'.", transaction.WalletId, transactionCode);
            throw new NotFoundException($"Không tìm thấy ví tiền với ID '{transaction.WalletId}'.");
        }

        // Cập nhật số dư và thông tin kiểm toán (Audit Trail)
        transaction.BalanceBefore = wallet.AvailableBalance;
        wallet.AvailableBalance += transaction.Amount;
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
            wallet.Id, transaction.Amount, wallet.AvailableBalance);

        return ApiResponse<bool>.SuccessResult(true, "Xử lý nạp tiền qua SePay thành công.");
    }
}
