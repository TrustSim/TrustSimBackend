using MediatR;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Financial.DTOs.Requests;

/// <summary>
/// Command mô phỏng can thiệp dữ liệu bất hợp pháp (Live Tamper Detection Demo) phục vụ Hội đồng.
/// </summary>
public record SimulateTamperTestCommand(
    long SequenceNumber = 0,
    decimal TamperedAmount = 99_000_000m
) : IRequest<TamperSimulationResultDto>;
