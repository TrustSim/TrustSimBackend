using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

public record SellerApproveRunnerUpRequest(
    bool IsApproved,
    string? RejectionReason = null);

public record SellerApproveRunnerUpCommand(
    Guid AuctionId,
    Guid SellerUserId,
    bool IsApproved,
    string? RejectionReason = null) : IRequest<AuctionResultDto>;
