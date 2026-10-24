using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record GetAuctionBidsQuery(Guid AuctionId, AuctionBidQueryFilter Filter) : IRequest<PagedResult<BidDto>>;
