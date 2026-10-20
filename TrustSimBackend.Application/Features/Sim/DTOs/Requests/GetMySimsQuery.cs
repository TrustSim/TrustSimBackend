using System;
using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách kho SIM thuộc quyền sở hữu của Người dùng hiện tại (có phân trang, sắp xếp & tìm kiếm).
/// </summary>
public record GetMySimsQuery(MySimsQueryFilter Filter) : IRequest<PagedResult<SimListItemDto>>
{
    // Được gán tự động từ ClaimsPrincipal trong Controller
    public Guid UserAccountId { get; set; }
}
