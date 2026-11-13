using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command của Admin duyệt hồ sơ bằng chứng sang tên SIM từ Bên Bán hoặc Bên Mua.
/// </summary>
public record ReviewSimTransferProofCommand(
    Guid TradeId,
    Guid AdminUserId,
    string TargetParty,
    bool IsApproved,
    string? RejectionReason
) : IRequest<SimTransferDetailsDto>;
