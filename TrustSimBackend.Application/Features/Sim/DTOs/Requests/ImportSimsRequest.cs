using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

public class ImportSimsRequest
{
    public IFormFile File { get; set; } = null!;
}
