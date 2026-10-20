using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query lấy danh sách lịch sử tất cả các lần nộp hồ sơ KYC của người dùng hiện tại (phân trang, sắp xếp).
/// </summary>
public record GetMyKycHistoryQuery(
    Guid UserId,
    BaseQueryFilter<KycSortBy> Filter
) : IRequest<PagedResult<IdentityVerificationDto>>;
