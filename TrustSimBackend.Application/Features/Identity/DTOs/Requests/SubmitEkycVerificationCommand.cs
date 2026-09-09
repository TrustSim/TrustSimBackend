using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command nộp hồ sơ eKYC tự động tích hợp AWS Rekognition.
/// </summary>
public record SubmitEkycVerificationCommand(
    IFormFile FrontCardImage,
    IFormFile? BackCardImage,
    IFormFile SelfieImage,
    Guid UserId = default
) : IRequest<EkycVerificationResultDto>;
