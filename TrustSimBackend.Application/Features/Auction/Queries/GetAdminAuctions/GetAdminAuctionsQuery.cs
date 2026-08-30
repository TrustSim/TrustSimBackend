using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.DTOs;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Queries.GetAdminAuctions;

/// <summary>
/// Query lấy danh sách toàn bộ phiên đấu giá cho Admin/Consultant (không giới hạn trạng thái).
/// Bao gồm thông tin Author để dễ theo dõi và quản lý.
/// </summary>
public record GetAdminAuctionsQuery(
    int PageIndex = 1,
    int PageSize = 10,
    AuctionType? AuctionType = null,
    AuctionStatus? Status = null,
    Guid? AuthorId = null,
    string? Search = null,
    DateTime? StartFrom = null,
    DateTime? StartTo = null
) : IRequest<ApiResponse<PagedResult<AuctionListItemDto>>>;

public class GetAdminAuctionsQueryHandler : IRequestHandler<GetAdminAuctionsQuery, ApiResponse<PagedResult<AuctionListItemDto>>>
{
    private readonly IAuctionRepository _auctionRepository;

    public GetAdminAuctionsQueryHandler(IAuctionRepository auctionRepository)
    {
        _auctionRepository = auctionRepository;
    }

    public async Task<ApiResponse<PagedResult<AuctionListItemDto>>> Handle(GetAdminAuctionsQuery request, CancellationToken cancellationToken)
    {
        var pageIndex = request.PageIndex < 1 ? 1 : request.PageIndex;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var (items, totalCount) = await _auctionRepository.GetAdminPagedAsync(
            pageIndex, pageSize,
            request.AuctionType,
            request.Status,
            request.AuthorId,
            request.Search,
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
            StartingPrice = a.StartingPrice,
            CurrentPrice = a.CurrentPrice,
            ReservePrice = a.ReservePrice,
            StartAt = a.StartAt,
            EndAt = a.EndAt,
            RegistrationStartAt = a.RegistrationStartAt,
            RegistrationEndAt = a.RegistrationEndAt,
            AuthorId = a.AuthorId,
            // Admin thấy đầy đủ thông tin Author
            AuthorName = a.Author?.UserProfile?.FullName ?? a.Author?.UserName,
            AuthorEmail = a.Author?.UserEmail
        }).ToList();

        var result = new PagedResult<AuctionListItemDto>(dtos, totalCount, pageIndex, pageSize);
        return ApiResponse<PagedResult<AuctionListItemDto>>.SuccessResult(result);
    }
}
