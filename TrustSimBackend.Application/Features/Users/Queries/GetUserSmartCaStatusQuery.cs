using MediatR;
using TrustSimBackend.Application.Features.Users.DTOs;

namespace TrustSimBackend.Application.Features.Users.Queries;

/// <summary>
/// Truy vấn kiểm tra trạng thái chứng thư số VNPT SmartCA của người dùng đăng nhập.
/// </summary>
public record GetUserSmartCaStatusQuery(Guid UserId) : IRequest<UserSmartCaStatusDto>;
