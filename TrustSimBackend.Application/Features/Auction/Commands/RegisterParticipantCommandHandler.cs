using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Features.Auction.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Auction.Commands;

public class RegisterParticipantCommandHandler : IRequestHandler<RegisterParticipantCommand, bool>
{
    private readonly IAuctionRepository _auctionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterParticipantCommandHandler(
        IAuctionRepository auctionRepository,
        IUnitOfWork unitOfWork)
    {
        _auctionRepository = auctionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(RegisterParticipantCommand request, CancellationToken cancellationToken)
    {
        var auction = await _auctionRepository.GetByIdAsync(request.AuctionId, cancellationToken);
        if (auction == null)
        {
            throw new NotFoundException($"Không tìm thấy phiên đấu giá với ID '{request.AuctionId}'.");
        }

        var existing = await _auctionRepository.GetParticipantAsync(request.AuctionId, request.UserId, cancellationToken);
        if (existing != null)
        {
            throw new BadRequestException("Bạn đã đăng ký tham gia phiên đấu giá này rồi.");
        }

        var participant = new AuctionParticipant
        {
            Id = Guid.NewGuid(),
            AuctionId = request.AuctionId,
            UserAccountId = request.UserId,
            Status = ParticipantStatus.REGISTERED,
            JoinedAt = DateTime.UtcNow
        };

        await _auctionRepository.AddParticipantAsync(participant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
