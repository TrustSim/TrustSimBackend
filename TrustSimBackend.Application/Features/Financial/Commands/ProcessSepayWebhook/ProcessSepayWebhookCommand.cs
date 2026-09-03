using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;

namespace TrustSimBackend.Application.Features.Financial.Commands.ProcessSepayWebhook;

/// <summary>
/// Command tiếp nhận và xử lý Webhook tự động biến động số dư từ cổng thanh toán SePay.
/// </summary>
public record ProcessSepayWebhookCommand(
    SepayWebhookDto WebhookPayload,
    string? AuthorizationHeader
) : IRequest<ApiResponse<bool>>;
