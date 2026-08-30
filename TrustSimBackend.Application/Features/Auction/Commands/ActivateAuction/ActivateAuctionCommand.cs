using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.ActivateAuction;

/// <summary>
/// Command kích hoạt phiên đấu giá từ SCHEDULE → ACTIVE (chỉ Admin).
/// Thường được trigger bởi Background Job (Hangfire/Quartz) đúng giờ StartAt,
/// nhưng Admin cũng có thể trigger thủ công.
/// </summary>
public record ActivateAuctionCommand(Guid AuctionId) : IRequest<ApiResponse<object>>;

public class ActivateAuctionCommandHandler : IRequestHandler<ActivateAuctionCommand, ApiResponse<object>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateAuctionCommandHandler(IAuctionRepository auctionRepository, IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<object>> Handle(ActivateAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        if (auction.AuctionStatus != AuctionStatus.SCHEDULE)
            throw new BadRequestException($"Chỉ có thể kích hoạt phiên đấu giá ở trạng thái SCHEDULE. Trạng thái hiện tại: {auction.AuctionStatus}.");

        auction.AuctionStatus = AuctionStatus.ACTIVE;

        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResult($"Phiên đấu giá '{auction.AuctionCode}' đã được kích hoạt thành công.");
    }
}
