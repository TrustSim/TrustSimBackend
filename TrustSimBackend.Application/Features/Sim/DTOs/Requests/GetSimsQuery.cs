using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record GetSimsQuery(
    string? Keyword = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    Guid? PrefixId = null,
    SimStatus? Status = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<SimNumberDto>>;
