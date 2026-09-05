using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query Admin lấy danh sách tất cả giao dịch rút tiền.
/// </summary>
public record AdminGetWithdrawalsQuery(
    TransactionStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<WalletTransactionDto>>;
