using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public class ImportCategoriesRequest
{
    public IFormFile File { get; set; } = null!;
}
