using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Query lấy danh sách phiên đấu giá có phân trang, lọc và sắp xếp động.
/// </summary>
public record GetAuctionsQuery(AuctionQueryFilter Filter) : IRequest<PagedResult<AuctionDto>>;
