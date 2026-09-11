using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ImportPhonePrefixesCommand(IFormFile File) : IRequest<ImportResult<Responses.PhonePrefixDto>>;
