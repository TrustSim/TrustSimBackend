using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Query Admin lấy danh sách tất cả giao dịch rút tiền.
/// </summary>
public record AdminGetWithdrawalsQuery(
    AdminWithdrawalQueryFilter Filter
) : IRequest<PagedResult<WalletTransactionDto>>;
