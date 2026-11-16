using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command của Admin duyệt hoặc từ chối trực tiếp một bộ bằng chứng (TransferProof).
/// </summary>
public record ReviewSimTransferProofCommand(
    Guid TransferProofId,
    Guid AdminUserId,
    bool IsApproved,
    string? RejectionReason
) : IRequest<SimTransferDetailsDto>;
