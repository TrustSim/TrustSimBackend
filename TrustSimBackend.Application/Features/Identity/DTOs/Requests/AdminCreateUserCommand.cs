using MediatR;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Application.Features.Identity.DTOs.Requests;

public record AdminCreateUserCommand(
    string UserName,
    string Email,
    string Password,
    UserRole Role,
    string FullName,
    string PhoneNumber,
    string? IdentityNumber,
    DateTime? DateOfBirth,
    string? Address
) : IRequest<AdminUserDetailDto>;
