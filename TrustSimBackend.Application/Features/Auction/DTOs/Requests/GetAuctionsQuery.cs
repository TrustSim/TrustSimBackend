using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record GetAuctionsQuery(
    string? Keyword = null,
    AuctionStatus? Status = null,
    AuctionType? Type = null,
    Guid? AuthorId = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<PagedResult<AuctionDto>>;
