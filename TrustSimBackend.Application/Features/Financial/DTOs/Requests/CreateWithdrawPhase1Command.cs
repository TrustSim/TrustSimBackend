using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command phục vụ kịch bản Demo Rút tiền Pha 1: Yêu cầu rút và Khóa số dư (Nợ 2010 USER_AVAILABLE / Có 2020 USER_LOCKED).
/// Trả về giao dịch ở trạng thái PENDING để hiển thị trên màn hình Sandbox.
/// </summary>
public record CreateWithdrawPhase1Command(
    Guid UserId,
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note = null
) : IRequest<WalletTransactionDto>;
