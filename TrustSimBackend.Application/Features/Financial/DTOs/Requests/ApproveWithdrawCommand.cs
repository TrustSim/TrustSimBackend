using MediatR;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command Admin duyệt yêu cầu rút tiền.
/// </summary>
public record ApproveWithdrawCommand(
    Guid TransactionId,
    string? Note = null,
    Guid AdminId = default
) : IRequest<bool>;
