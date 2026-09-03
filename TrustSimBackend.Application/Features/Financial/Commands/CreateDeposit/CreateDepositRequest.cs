namespace TrustSimBackend.Application.Features.Financial.Commands.CreateDeposit;

/// <summary>
/// DTO Request nạp tiền vào ví gửi từ Client.
/// </summary>
public record CreateDepositRequest(
    decimal Amount,
    string? Note
);
