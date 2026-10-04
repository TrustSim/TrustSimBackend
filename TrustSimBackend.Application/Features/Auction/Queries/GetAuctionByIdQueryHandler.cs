using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries;

public class GetAuctionByIdQueryHandler : IRequestHandler<GetAuctionByIdQuery, AuctionDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public GetAuctionByIdQueryHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<AuctionDto> Handle(GetAuctionByIdQuery request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetDetailByIdAsync(request.Id, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.Id}'.");
        }

        var isUserRegistered = _currentUserService.IsAuthenticated 
            && _currentUserService.UserId.HasValue 
            && auction.Participants.Any(p => p.UserAccountId == _currentUserService.UserId.Value);

        var auctionResult = await _unitOfWork.Auctions.GetResultByAuctionIdAsync(request.Id, cancellationToken);
        var resultDto = auctionResult == null ? null : new AuctionResultDto
        {
            Id = auctionResult.Id,
            AuctionId = auctionResult.AuctionId,
            WinnerId = auctionResult.WinnerId,
            WinnerUserName = auctionResult.Winner?.UserAccount?.UserName,
            FinalPrice = auctionResult.FinalPrice,
            RunnerUpParticipantId = auctionResult.RunnerUpParticipantId,
            RunnerUpUserName = auctionResult.RunnerUpParticipant?.UserAccount?.UserName,
            RunnerUpFinalPrice = auctionResult.RunnerUpFinalPrice,
            AuctionResultStatus = auctionResult.AuctionResultStatus,
            PaymentDeadline = auctionResult.PaymentDeadline,
            IsRunnerUpOffered = auctionResult.IsRunnerUpOffered,
            RunnerUpPaymentDeadline = auctionResult.RunnerUpPaymentDeadline,
            SettledAt = auctionResult.SettledAt,
            DeterminedAt = auctionResult.DeterminedAt,
            ConfirmedAt = auctionResult.ConfirmedAt,
            CancelledReason = auctionResult.CancelledReason
        };

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
            TotalBids = auction.Bids.Count,

            // Thông tin quy chế bổ sung
            MinBidStep = auction.AuctionRule?.MinimumBidIncrement ?? 500_000,
            DepositAmount = auction.AuctionRule?.DepositAmount ?? 0,
            ExtensionDurationSeconds = auction.AuctionRule?.ExtensionDurationSeconds ?? 60f,
            DutchDecreaseAmount = auction.AuctionRule?.DutchDecreaseAmount ?? 0,
            DutchDecreaseIntervalSeconds = auction.AuctionRule?.DutchDecreaseIntervalSeconds ?? 0,

            // Thông tin SIM bổ sung
            FormatRhythmName = auction.SimNumber?.FormatRhythm?.FormatRhythmName,
            Categories = auction.SimNumber?.SimCategoryMappings?
                .Select(m => m.SimCategory?.CategoryName)
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => c!)
                .ToList() ?? new List<string>(),

            // Trạng thái của người dùng
            IsUserRegistered = isUserRegistered,

            // Kết quả phiên đấu giá
            Result = resultDto
        };
    }
}
