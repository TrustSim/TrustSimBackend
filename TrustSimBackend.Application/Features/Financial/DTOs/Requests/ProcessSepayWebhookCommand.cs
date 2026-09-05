using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Webhook biến động số dư từ SePay.
/// </summary>
public record ProcessSepayWebhookCommand(
    SepayWebhookDto WebhookPayload,
    string? AuthorizationHeader = null
) : IRequest<bool>;
