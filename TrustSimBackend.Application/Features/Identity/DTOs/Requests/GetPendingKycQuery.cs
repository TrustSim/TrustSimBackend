using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query danh sách hồ sơ KYC dành cho Quản trị viên.
/// </summary>
public record GetPendingKycQuery(
    AdminKycQueryFilter Filter
) : IRequest<PagedResult<IdentityVerificationDto>>;
