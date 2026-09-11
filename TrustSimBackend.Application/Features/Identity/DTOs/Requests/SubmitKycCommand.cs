using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command nộp hồ sơ KYC xác minh danh tính.
/// </summary>
public record SubmitKycCommand(
    string IdentityNumber,
    string? AvatarImg = null,
    Guid UserId = default
) : IRequest<IdentityVerificationDto>;
