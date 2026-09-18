using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách toàn bộ kho SIM trên hệ thống dành cho Admin (có phân trang và lọc đa tiêu chí).
/// </summary>
public record GetAdminSimsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    SimStatus? Status = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    string? Search = null
) : IRequest<PagedResult<SimListItemDto>>;
