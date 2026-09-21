using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command khởi tạo Payment Intent nạp tiền và sinh mã VietQR.
/// </summary>
public record CreateDepositIntentCommand(
    Guid UserId,
    decimal Amount,
    string? IdempotencyKey = null
) : IRequest<PaymentIntentDto>;
