using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ImportSimsCommand(
    IFormFile File,
    Guid UserId,
    bool AutoVerify = false
) : IRequest<ImportResult<SimNumberDto>>;
