using MediatR;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command xử lý Webhook Callback từ Cổng chi hộ ngân hàng đối với yêu cầu rút tiền (Pha 2).
/// </summary>
public record ProcessWithdrawCallbackCommand(ProcessWithdrawCallbackRequest CallbackData) : IRequest<bool>;
