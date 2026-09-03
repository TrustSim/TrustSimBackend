namespace TrustSimBackend.Application.Features.Financial.Commands.CreateWithdraw;

/// <summary>
/// DTO Request rút tiền từ ví về tài khoản ngân hàng của người dùng.
/// </summary>
public record CreateWithdrawRequest(
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note
);
