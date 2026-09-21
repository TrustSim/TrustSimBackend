using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 4 (Pha 2B): Payout ngân hàng thất bại hoặc bị hủy -> Tự động hoàn tiền vào ví khả dụng.
/// Ghi nhận Sổ cái kép hoàn tiền (Nợ 2020 USER_LOCKED / Có 2010 USER_AVAILABLE).
/// </summary>
public record ProcessWithdrawFailedCommand(
    string TransactionCode,
    string FailureReason
) : IRequest<WalletTransactionDto>;
