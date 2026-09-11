using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query lấy trạng thái KYC gần nhất của người dùng hiện tại.
/// </summary>
public record GetMyKycStatusQuery(
    Guid UserId
) : IRequest<IdentityVerificationDto>;
