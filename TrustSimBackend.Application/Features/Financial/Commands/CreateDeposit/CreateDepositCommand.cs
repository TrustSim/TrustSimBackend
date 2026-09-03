using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;

namespace TrustSimBackend.Application.Features.Financial.Commands.CreateDeposit;

/// <summary>
/// Command khởi tạo yêu cầu nạp tiền vào ví người dùng qua SePay VietQR.
/// </summary>
public record CreateDepositCommand(
    Guid UserId,
    decimal Amount,
    string? Note
) : IRequest<ApiResponse<DepositResponseDto>>;
