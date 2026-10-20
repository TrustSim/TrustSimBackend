using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query tìm kiếm và lọc danh sách SIM số đẹp công khai trên sàn (phân trang, lọc theo nhà mạng, thể loại, đầu số, giá, sắp xếp động).
/// </summary>
public record GetSimsQuery(SimPublicQueryFilter Filter) : IRequest<PagedResult<SimNumberDto>>;
