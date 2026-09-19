using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Financial.Commands;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;

namespace TrustSimBackend.Infrastructure.Services.Financial;

/// <summary>
/// Triển khai Cổng giả lập chi hộ Ngân hàng (Mock Bank Payout Sandbox).
/// Phục vụ kịch bản Demo rút tiền tự động không cần phụ thuộc ngân hàng thật.
/// </summary>
public class MockBankPayoutGatewayService : IMockBankPayoutGatewayService
{
    private readonly ISender _mediator;
    private readonly ILogger<MockBankPayoutGatewayService> _logger;

    public MockBankPayoutGatewayService(
        ISender mediator,
        ILogger<MockBankPayoutGatewayService> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<bool> SimulateBankPayoutCallbackAsync(
        string transactionCode,
        bool isSuccess,
        string? failureReason = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Mock Bank Gateway] Bắt đầu giả lập Payout Webhook cho GD '{Code}' (Success: {Success})",
            transactionCode, isSuccess);

        var callbackRequest = new ProcessWithdrawCallbackRequest
        {
            TransactionCode = transactionCode.Trim(),
            IsSuccess = isSuccess,
            ExternalReference = $"MOCK-BANK-PAYOUT-{Random.Shared.Next(100000, 999999)}",
            FailureReason = failureReason
        };

        return await _mediator.Send(new ProcessWithdrawCallbackCommand(callbackRequest), cancellationToken);
    }
}
