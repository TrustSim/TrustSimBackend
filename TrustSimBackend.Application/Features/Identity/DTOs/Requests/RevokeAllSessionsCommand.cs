using MediatR;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

/// <summary>
/// Command đăng xuất khỏi toàn bộ các thiết bị (thu hồi tất cả active tokens).
/// </summary>
public record RevokeAllSessionsCommand(
    Guid UserId,
    string? IpAddress = null
) : IRequest<bool>;
