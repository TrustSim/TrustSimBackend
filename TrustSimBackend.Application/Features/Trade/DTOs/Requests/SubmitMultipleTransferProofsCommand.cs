using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Features.Trade.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Trade.DTOs.Requests;

/// <summary>
/// Command nộp nhiều bằng chứng chuyển nhượng SIM (Bên Bán hoặc Bên Mua).
/// </summary>
public record SubmitMultipleTransferProofsCommand(
    Guid TradeId,
    Guid UserId,
    List<IFormFile> Files,
    TransferProofType ProofType,
    string? Description
) : IRequest<SimTransferDetailsDto>;
