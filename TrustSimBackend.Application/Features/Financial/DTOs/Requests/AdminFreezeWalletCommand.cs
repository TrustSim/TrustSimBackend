using MediatR;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

public record AdminFreezeWalletCommand(Guid WalletId, bool Freeze) : IRequest<bool>;
