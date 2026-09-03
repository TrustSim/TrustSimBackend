using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateWithdraw;

/// <summary>
/// Command tạo yêu cầu rút tiền từ ví về tài khoản ngân hàng.
/// </summary>
public record CreateWithdrawCommand(
    Guid UserId,
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note
) : IRequest<ApiResponse<WalletTransactionDto>>;
