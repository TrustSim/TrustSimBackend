using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Nghiệp vụ 3 (Pha 2A): Chi trả rút tiền thành công từ cổng ngân hàng.
/// Ghi nhận Sổ cái kép (Nợ 2020 USER_LOCKED / Có 1010 EXTERNAL_GATEWAY_CLEARING).
/// </summary>
public record ProcessWithdrawSuccessCommand(
    string TransactionCode,
    string? ExternalBankRef = null
) : IRequest<WalletTransactionDto>;
