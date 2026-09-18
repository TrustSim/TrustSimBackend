using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class AdminCancelAuctionCommandHandler : IRequestHandler<AdminCancelAuctionCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminCancelAuctionCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(AdminCancelAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _unitOfWork.Auctions.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null) throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        if (auction.AuctionStatus == AuctionStatus.END)
        {
            throw new BadRequestException("Không thể hủy phiên đấu giá đã kết thúc thành công.");
        }

        auction.AuctionStatus = AuctionStatus.CANCELLED;
        auction.UpdatedDate = DateTime.UtcNow;

        _unitOfWork.Auctions.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
