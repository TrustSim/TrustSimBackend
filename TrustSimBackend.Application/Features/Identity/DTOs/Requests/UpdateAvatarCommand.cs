using MediatR;
using Microsoft.AspNetCore.Http;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command tải lên và cập nhật ảnh đại diện người dùng.
/// </summary>
public record UpdateAvatarCommand(
    IFormFile File,
    Guid UserId = default
) : IRequest<string>;

