using MediatR;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command Admin từ chối yêu cầu rút tiền.
/// </summary>
public record RejectWithdrawCommand(
    Guid TransactionId,
    string Reason,
    Guid AdminId = default
) : IRequest<bool>;
