using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Financial.Commands.RejectWithdraw;

/// <summary>
/// Command dành cho Admin từ chối yêu cầu rút tiền và hoàn lại số tiền vào ví người dùng.
/// </summary>
public record RejectWithdrawCommand(
    Guid TransactionId,
    Guid AdminUserId,
    string Reason
) : IRequest<ApiResponse<bool>>;
