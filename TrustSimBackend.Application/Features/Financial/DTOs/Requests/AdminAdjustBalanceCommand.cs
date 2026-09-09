using MediatR;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminAdjustBalanceCommand(
    Guid WalletId,
    decimal Amount,
    string Reason,
    Guid AdminId
) : IRequest<bool>;
