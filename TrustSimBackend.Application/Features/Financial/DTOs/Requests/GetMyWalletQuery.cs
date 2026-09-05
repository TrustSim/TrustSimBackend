using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query lấy thông tin ví và số dư của người dùng theo UserId.
/// </summary>
public record GetMyWalletQuery(
    Guid UserId
) : IRequest<WalletDto>;
