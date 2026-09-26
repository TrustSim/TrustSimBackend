using MediatR;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Command Người về nhì từ chối mua SIM khi được gửi đề nghị chuyển quyền.
/// </summary>
public record RejectRunnerUpOfferCommand(
    Guid AuctionId,
    Guid UserId = default
) : IRequest<bool>;
