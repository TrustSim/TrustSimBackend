using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách quyền sở hữu SIM của người dùng hiện tại kèm thông tin xác minh (phân trang, sắp xếp).
/// </summary>
public record GetMySimOwnershipsQuery(
    Guid UserId,
    BaseQueryFilter<SimOwnershipSortBy> Filter
) : IRequest<PagedResult<SimOwnershipDto>>;
