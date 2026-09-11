using MediatR;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record AdminExportAuctionsQuery(
    string? Keyword = null,
    AuctionStatus? Status = null,
    AuctionType? Type = null
) : IRequest<byte[]>;
