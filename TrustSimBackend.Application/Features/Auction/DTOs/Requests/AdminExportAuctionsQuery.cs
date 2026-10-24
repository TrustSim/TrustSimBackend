using MediatR;
using TrustSimBackend.Domain.Filters;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record AdminExportAuctionsQuery(AuctionQueryFilter? Filter = null) : IRequest<byte[]>;
