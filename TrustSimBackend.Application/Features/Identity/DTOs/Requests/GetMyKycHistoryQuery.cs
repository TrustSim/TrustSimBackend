using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query lấy danh sách lịch sử tất cả các lần nộp hồ sơ KYC của người dùng hiện tại.
/// </summary>
public record GetMyKycHistoryQuery(Guid UserId) : IRequest<IReadOnlyList<IdentityVerificationDto>>;
