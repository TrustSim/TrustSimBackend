using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;

namespace TrustSimBackend.Application.Features.Financial.Queries.GetMyWallet;

/// <summary>
/// Query lấy thông tin ví và số dư của người dùng hiện tại.
/// </summary>
public record GetMyWalletQuery(
    Guid UserId
) : IRequest<ApiResponse<WalletDto>>;
