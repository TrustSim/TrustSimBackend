using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách toàn bộ kho SIM trên hệ thống dành cho Admin (có phân trang, sắp xếp và lọc đa tiêu chí).
/// </summary>
public record GetAdminSimsQuery(AdminSimsQueryFilter Filter) : IRequest<PagedResult<SimListItemDto>>;
