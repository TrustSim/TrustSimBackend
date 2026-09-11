using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command yêu cầu rút tiền từ ví.
/// </summary>
public record CreateWithdrawCommand(
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note = null,
    Guid UserId = default
) : IRequest<WalletTransactionDto>;
