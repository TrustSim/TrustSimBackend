using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Financial.Commands.ApproveWithdraw;

/// <summary>
/// Command dành cho Admin duyệt hoàn tất yêu cầu rút tiền của người dùng.
/// </summary>
public record ApproveWithdrawCommand(
    Guid TransactionId,
    Guid AdminUserId,
    string? Note
) : IRequest<ApiResponse<bool>>;
