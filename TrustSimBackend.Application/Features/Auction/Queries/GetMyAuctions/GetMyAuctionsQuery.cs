using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries.GetMyAuctions;

/// <summary>
/// Query lấy danh sách phiên đấu giá của chính Người dùng (Author/Seller).
/// </summary>
public record GetMyAuctionsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    AuctionStatus? Status = null
) : IRequest<ApiResponse<PagedResult<AuctionListItemDto>>>
{
    public Guid AuthorId { get; set; }
}

public class GetMyAuctionsQueryHandler : IRequestHandler<GetMyAuctionsQuery, ApiResponse<PagedResult<AuctionListItemDto>>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetMyAuctionsQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<PagedResult<AuctionListItemDto>>> Handle(GetMyAuctionsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 50 ? 10 : request.PageSize;

        var (items, totalCount) = await _auctionRepository.GetMyAuctionsPagedAsync(
            request.AuthorId, pageIndex, pageSize, request.Status, cancellationToken);

        var dtos = items.Select(a => new AuctionListItemDto
        {
            AuctionId = a.Id,
            AuctionCode = a.AuctionCode,
            AuctionType = a.AuctionType,
            AuctionStatus = a.AuctionStatus,
            SimNumberId = a.SimNumberId,
            PhoneNumber = a.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = a.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName,
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = a.ReservePrice,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            RegistrationStartAt = a.RegistrationStartAt,
            RegistrationEndAt = a.RegistrationEndAt,
            AuthorId = a.AuthorId
        }).ToList();

        var result = new PagedResult<AuctionListItemDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResult<AuctionListItemDto>>.SuccessResult(result);
    }
}
