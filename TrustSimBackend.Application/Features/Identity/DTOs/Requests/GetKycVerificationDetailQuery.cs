using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Query lấy chi tiết một hồ sơ KYC kèm bằng chứng ảnh và kết quả AI cho Admin.
/// </summary>
public record GetKycVerificationDetailQuery(Guid VerificationId) : IRequest<IdentityVerificationDetailDto>;
