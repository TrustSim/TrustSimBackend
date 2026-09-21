using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command MediatR xử lý yêu cầu rút tiền từ ví.
/// </summary>
public record CreateWithdrawCommand(
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note,
    Guid UserId
) : IRequest<WalletTransactionDto>;
