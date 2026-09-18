using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy thông tin chi tiết một số SIM theo ID.
/// </summary>
public record GetSimDetailQuery(Guid Id) : IRequest<SimDetailDto>;
