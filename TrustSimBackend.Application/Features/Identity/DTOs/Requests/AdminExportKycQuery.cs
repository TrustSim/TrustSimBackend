using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminExportKycQuery(AdminKycQueryFilter? Filter = null) : IRequest<byte[]>;
