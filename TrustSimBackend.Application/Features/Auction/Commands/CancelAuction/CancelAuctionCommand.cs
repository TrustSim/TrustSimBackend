using FluentValidation;
using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands.CancelAuction;

/// <summary>
/// Command hủy phiên đấu giá.
/// - Author chỉ được hủy khi phiên đang SCHEDULE (chưa bắt đầu).
/// - Admin có thể hủy ở bất kỳ trạng thái nào ngoại trừ END.
/// </summary>
public record CancelAuctionCommand(
    Guid AuctionId,
    string? CancellationReason
) : IRequest<ApiResponse<object>>
{
    public Guid RequesterId { get; set; }
    public bool IsAdmin { get; set; }
}

public class CancelAuctionCommandValidator : AbstractValidator<CancelAuctionCommand>
{
    public CancelAuctionCommandValidator()
    {
        RuleFor(x => x.AuctionId).NotEmpty().WithMessage("ID phiên đấu giá không hợp lệ.");

        RuleFor(x => x.CancellationReason)
            .MaximumLength(500).WithMessage("Lý do hủy không quá 500 ký tự.");
    }
}

public class CancelAuctionCommandHandler : IRequestHandler<CancelAuctionCommand, ApiResponse<object>>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAuctionCommandHandler(IAuctionRepository auctionRepository, IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<object>> Handle(CancelAuctionCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");

        // Kiểm tra quyền
        if (!request.IsAdmin && auction.AuthorId != request.RequesterId)
            throw new ForbiddenAccessException("Bạn không có quyền hủy phiên đấu giá này.");

        // Kiểm tra trạng thái có thể hủy
        if (auction.AuctionStatus == AuctionStatus.END)
            throw new BadRequestException("Không thể hủy phiên đấu giá đã kết thúc.");

        if (auction.AuctionStatus == AuctionStatus.CANCELLED)
            throw new BadRequestException("Phiên đấu giá này đã bị hủy trước đó.");

        // User thường chỉ được hủy khi SCHEDULE
        if (!request.IsAdmin && auction.AuctionStatus == AuctionStatus.ACTIVE)
            throw new ForbiddenAccessException("Phiên đấu giá đã bắt đầu, chỉ quản trị viên mới có quyền hủy.");

        // Cập nhật trạng thái
        auction.AuctionStatus = AuctionStatus.CANCELLED;
        auction.IsActive = false;
        // TODO: Lưu CancellationReason vào field (nếu thêm field sau)

        _auctionRepository.Update(auction);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.SuccessResult("Phiên đấu giá đã được hủy thành công.");
    }
}
