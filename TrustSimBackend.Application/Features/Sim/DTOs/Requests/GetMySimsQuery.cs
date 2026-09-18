using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách kho SIM thuộc quyền sở hữu của Người dùng hiện tại (có phân trang & tìm kiếm).
/// </summary>
public record GetMySimsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    SimStatus? Status = null,
    string? Search = null
) : IRequest<PagedResult<SimListItemDto>>
{
    // Được gán tự động từ ClaimsPrincipal trong Controller
    public Guid UserAccountId { get; set; }
}
