using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminExportKycQuery(
    VerificationStatus? Status = null
) : IRequest<byte[]>;
