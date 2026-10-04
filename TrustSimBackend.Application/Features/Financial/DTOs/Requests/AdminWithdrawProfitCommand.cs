using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command MediatR xử lý Admin rút tiền lợi nhuận sàn về tài khoản cá nhân.
/// </summary>
public record AdminWithdrawProfitCommand(
    decimal Amount,
    string DestinationBankCode,
    string DestinationBankName,
    string DestinationAccountNumber,
    string DestinationAccountHolderName,
    string? Note,
    Guid AdminUserId,
    string? IdempotencyKey = null
) : IRequest<AdminProfitWithdrawResultDto>;
