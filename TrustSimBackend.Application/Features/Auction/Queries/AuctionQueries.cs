using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public record GetAuctionsQuery(
    string? Keyword = null,
    AuctionStatus? Status = null,
    AuctionType? Type = null,
    Guid? AuthorId = null,
    int PageIndex = 1,
    int PageSize = 20
) : IRequest<ApiResponse<PagedResult<AuctionDto>>>;

public class GetAuctionsQueryHandler : IRequestHandler<GetAuctionsQuery, ApiResponse<PagedResult<AuctionDto>>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAuctionsQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<PagedResult<AuctionDto>>> Handle(GetAuctionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _auctionRepository.GetPagedAsync(
            request.Keyword,
            request.Status,
            request.Type,
            request.AuthorId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var dtos = items.Select(a => new AuctionDto
        {
            Id = a.Id,
            AuctionCode = a.AuctionCode,
            AuctionType = a.AuctionType,
            AuctionStatus = a.AuctionStatus,
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = a.ReservePrice,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            RegistrationStartAt = a.RegistrationStartAt,
            RegistrationEndAt = a.RegistrationEndAt,
            AuthorId = a.AuthorId,
            AuthorName = a.Author?.UserProfile?.FullName ?? a.Author?.UserName ?? string.Empty,
            SimNumberId = a.SimNumberId,
            SimPhoneNumber = a.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = a.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName ?? string.Empty,
            TotalParticipants = a.Participants.Count,
            TotalBids = a.Bids.Count
        }).ToList();

        var pagedResult = PagedResult<AuctionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
        return ApiResponse<PagedResult<AuctionDto>>.SuccessResult(pagedResult, "Lấy danh sách phiên đấu giá thành công.");
    }
}

public record GetAuctionByIdQuery(Guid Id) : IRequest<ApiResponse<AuctionDto>>;

public class GetAuctionByIdQueryHandler : IRequestHandler<GetAuctionByIdQuery, ApiResponse<AuctionDto>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAuctionByIdQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<AuctionDto>> Handle(GetAuctionByIdQuery request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.Id, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.Id}'.");
        }

        var dto = new AuctionDto
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

        return ApiResponse<AuctionDto>.SuccessResult(dto, "Lấy thông tin phiên đấu giá thành công.");
    }
}

public record GetAuctionBidsQuery(Guid AuctionId, int Top = 50) : IRequest<ApiResponse<IReadOnlyList<BidDto>>>;

public class GetAuctionBidsQueryHandler : IRequestHandler<GetAuctionBidsQuery, ApiResponse<IReadOnlyList<BidDto>>>
{
    private readonly IBidRepository _bidRepository;

    public GetAuctionBidsQueryHandler(IBidRepository bidRepository)
    {
        _bidRepository = bidRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<BidDto>>> Handle(GetAuctionBidsQuery request, CancellationToken cancellationToken)
    {
        var bids = await _bidRepository.GetBidsByAuctionIdAsync(request.AuctionId, request.Top, cancellationToken);
        var dtos = bids.Select(b => new BidDto
        {
            Id = b.Id,
            AuctionId = b.AuctionId,
            ParticipantId = b.ParticipantId,
            UserAccountId = b.Participant?.UserAccountId ?? Guid.Empty,
            UserNameMasked = b.Participant?.UserAccount?.UserName != null && b.Participant.UserAccount.UserName.Length > 3 
                ? b.Participant.UserAccount.UserName[..2] + "***" + b.Participant.UserAccount.UserName[^1..] 
                : "***",
            BidAmount = b.BidAmount,
            PlacedAt = b.PlacedAt,
            Status = b.Status
        }).ToList();

        return ApiResponse<IReadOnlyList<BidDto>>.SuccessResult(dtos, "Lấy lịch sử trả giá thành công.");
    }
}
