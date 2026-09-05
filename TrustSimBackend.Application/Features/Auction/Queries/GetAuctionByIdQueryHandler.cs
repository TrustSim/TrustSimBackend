using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class GetAuctionByIdQueryHandler : IRequestHandler<GetAuctionByIdQuery, AuctionDto>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAuctionByIdQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<AuctionDto> Handle(GetAuctionByIdQuery request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.Id}'.");
        }

        return new AuctionDto
        {
            Id = auction.Id,
            AuctionCode = auction.AuctionCode,
            AuctionType = auction.AuctionType,
            AuctionStatus = auction.AuctionStatus,
            StartingPrice = auction.StartingPrice,
            CurrentPrice = auction.CurrentPrice,
            ReservePrice = auction.ReservePrice,
            StartAt = auction.StartAt,
            EndAt = auction.EndAt,
            RegistrationStartAt = auction.RegistrationStartAt,
            RegistrationEndAt = auction.RegistrationEndAt,
            AuthorId = auction.AuthorId,
            AuthorName = auction.Author?.UserProfile?.FullName ?? auction.Author?.UserName ?? string.Empty,
            SimNumberId = auction.SimNumberId,
            SimPhoneNumber = auction.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = auction.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            TotalParticipants = auction.Participants.Count,
            TotalBids = auction.Bids.Count
        };
    }
}
