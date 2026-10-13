using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class GetAuctionsQueryHandler : IRequestHandler<GetAuctionsQuery, PagedResult<AuctionDto>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetAuctionsQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<PagedResult<AuctionDto>> Handle(GetAuctionsQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await _unitOfWork.Auctions.GetPagedAsync(
            request.Keyword,
            request.Status,
            request.Type,
            request.AuthorId,
            request.PageIndex,
            request.PageSize,
            cancellationToken);

        var currentUserId = _currentUserService.IsAuthenticated ? _currentUserService.UserId : null;

        var dtos = items.Select(a => new AuctionDto
        {
            Id = a.Id,
            AuctionCode = a.AuctionCode,
            AuctionType = a.AuctionType,
            AuctionStatus = a.AuctionStatus,
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = (a.AuctionType == AuctionType.DUTCH && (!currentUserId.HasValue || (a.AuthorId != currentUserId.Value && _currentUserService.Role != "ADMIN")))
                ? 0m
                : a.ReservePrice,
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
            TotalBids = a.Bids.Count,
            DepositAmount = a.AuctionRule?.DepositAmount ?? 0,
            MinBidStep = a.AuctionRule?.MinimumBidIncrement ?? 500_000,
            DutchDecreaseAmount = a.AuctionRule?.DutchDecreaseAmount ?? 0,
            DutchDecreaseIntervalSeconds = a.AuctionRule?.DutchDecreaseIntervalSeconds ?? 0,
            IsUserRegistered = currentUserId.HasValue && a.Participants.Any(p => p.UserAccountId == currentUserId.Value)
        }).ToList();

        return PagedResult<AuctionDto>.Create(dtos, totalCount, request.PageIndex, request.PageSize);
    }
}

