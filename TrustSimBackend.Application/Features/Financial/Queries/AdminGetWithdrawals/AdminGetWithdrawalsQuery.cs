using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.Queries.AdminGetWithdrawals;

/// <summary>
/// Query dành cho Admin lấy danh sách các yêu cầu rút tiền trong toàn hệ thống.
/// </summary>
public record AdminGetWithdrawalsQuery(
    TransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<WalletTransactionDto>>>;
