using MediatR;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command dành cho Bên Bán nộp ảnh biên bản sang tên SIM của nhà mạng.
/// </summary>
public record SubmitTransferProofCommand(
    Guid TradeId,
    Guid UserId,
    string ImageUrl,
    string? Description
) : IRequest<SimTransferDetailsDto>;
