using MediatR;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record SeedSimMasterDataCommand : IRequest<string>;
