using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command cho phép Người Bán hoặc Người Mua thu hồi (hủy) bộ bằng chứng đang ở trạng thái PENDING trước khi Admin duyệt.
/// </summary>
public record WithdrawTransferProofCommand(
    Guid TransferProofId,
    Guid UserId
) : IRequest<SimTransferDetailsDto>;
