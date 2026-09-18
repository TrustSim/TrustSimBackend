using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Sim.DTOs.Responses;

namespace TrustSimBackend.Application.Features.Sim.DTOs.Requests;

/// <summary>
/// Command đăng ký số SIM mới vào hệ thống cho Người dùng (Seller).
/// </summary>
public record RegisterSimCommand(
    string PhoneNumber,
    Guid FormatRhythmId,
    List<Guid>? CategoryIds = null,
    Guid? PhonePrefixId = null
) : IRequest<SimDetailDto>
{
    // Được gán từ Current User (ClaimsPrincipal trong Controller)
    public Guid UserAccountId { get; set; }
}
