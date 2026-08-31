using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Auction.States;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.ActivateAuction;

/// <summary>
/// Command kích hoạt phiên đấu giá SCHEDULE → ACTIVE.
/// Thường được gọi từ Background Service tự động, hoặc Admin kích hoạt thủ công.
/// ★ State Pattern: Giao toàn bộ kiểm tra hợp lệ + chuyển trạng thái cho IAuctionState.Activate().
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

        // ★ State Pattern: ScheduledAuctionState.Activate() → chuyển sang ACTIVE
        //                  ActiveAuctionState.Activate() → throw "đang ACTIVE rồi"
        //                  EndedAuctionState.Activate() → throw "đã kết thúc"
        var state = AuctionStateContext.GetState(auction.AuctionStatus);
        state.Activate(auction);

        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResult($"Phiên đấu giá '{auction.AuctionCode}' đã được kích hoạt thành công.");
    }
}
