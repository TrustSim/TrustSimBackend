using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command phê duyệt hoặc từ chối hồ sơ KYC.
/// </summary>
public record ReviewKycCommand(
    VerificationStatus Status,
    string? RejectionReason = null,
    Guid VerificationId = default,
    Guid? AdminUserId = default
) : IRequest<bool>;

