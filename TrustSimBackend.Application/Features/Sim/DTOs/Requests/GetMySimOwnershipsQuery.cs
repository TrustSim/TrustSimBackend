using MediatR;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Query lấy danh sách quyền sở hữu SIM của người dùng hiện tại kèm thông tin xác minh.
/// </summary>
public record GetMySimOwnershipsQuery(Guid UserId) : IRequest<IReadOnlyList<SimOwnershipDto>>;
