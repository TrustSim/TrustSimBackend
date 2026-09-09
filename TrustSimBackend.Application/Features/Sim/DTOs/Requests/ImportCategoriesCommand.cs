using MediatR;
using Microsoft.AspNetCore.Http;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public record ImportCategoriesCommand(IFormFile File) : IRequest<ImportResult<SimCategoryDto>>;
