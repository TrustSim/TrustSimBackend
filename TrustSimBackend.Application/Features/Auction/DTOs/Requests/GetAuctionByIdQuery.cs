using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record GetAuctionByIdQuery(Guid Id) : IRequest<AuctionDto>;
