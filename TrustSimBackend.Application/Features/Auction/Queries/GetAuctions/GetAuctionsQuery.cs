using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries.GetAuctions;

/// <summary>
/// Query lấy danh sách phiên đấu giá phân trang cho Public (chỉ SCHEDULE + ACTIVE).
/// Hỗ trợ lọc theo: loại hình, nhà mạng, danh mục, giá, khoảng thời gian, từ khóa.
/// </summary>
public record GetAuctionsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    AuctionType? AuctionType = null,
    AuctionStatus? Status = null,
    Guid? CarrierId = null,
    Guid? CategoryId = null,
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    DateTime? StartFrom = null,
    DateTime? StartTo = null
) : IRequest<ApiResponse<PagedResult<AuctionListItemDto>>>;

public class GetAuctionsQueryHandler : IRequestHandler<GetAuctionsQuery, ApiResponse<PagedResult<AuctionListItemDto>>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAuctionsQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<PagedResult<AuctionListItemDto>>> Handle(GetAuctionsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 50 ? 10 : request.PageSize;

        var (items, totalCount) = await _auctionRepository.GetPublicPagedAsync(
            pageIndex, pageSize,
            request.AuctionType,
            request.Status,
            request.CarrierId,
            request.CategoryId,
            request.Search,
            request.MinPrice,
            request.MaxPrice,
            request.StartFrom,
            request.StartTo,
            cancellationToken);

        var dtos = items.Select(a => new AuctionListItemDto
        {
            AuctionId = a.Id,
            AuctionCode = a.AuctionCode,
            AuctionType = a.AuctionType,
            AuctionStatus = a.AuctionStatus,
            SimNumberId = a.SimNumberId,
            PhoneNumber = a.SimNumber?.PhoneNumber ?? string.Empty,
            CarrierName = a.SimNumber?.PhonePrefix?.SimCarrier?.CarrierName,
            FormatRhythmName = a.SimNumber?.FormatRhythm?.FormatRhythmName,
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = a.ReservePrice,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            RegistrationStartAt = a.RegistrationStartAt,
            RegistrationEndAt = a.RegistrationEndAt,
            AuthorId = a.AuthorId
            // AuthorName/Email ẩn khỏi public list
        }).ToList();

        var result = new PagedResult<AuctionListItemDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResult<AuctionListItemDto>>.SuccessResult(result);
    }
}
