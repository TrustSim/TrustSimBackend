using MediatR;
using TrustSimBackend.Application.Features.Auction.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Auction.DTOs.Requests;

/// <summary>
/// Query lấy danh sách các giao dịch / phiên đấu giá đang cần người dùng xử lý (Pending Settlements).
/// </summary>
/// <param name="UserId">ID của người dùng hiện tại</param>
public record GetMyPendingSettlementsQuery(Guid UserId) : IRequest<IReadOnlyList<PendingSettlementDto>>;
