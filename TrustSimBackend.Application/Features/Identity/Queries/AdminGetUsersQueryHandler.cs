using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

public class AdminGetUsersQueryHandler : IRequestHandler<AdminGetUsersQuery, PagedResult<AdminUserDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminGetUsersQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AdminUserDetailDto>> Handle(AdminGetUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, totalCount) = await _unitOfWork.Users.GetPagedAsync(
            request.Filter,
            cancellationToken);

        var dtos = users.Select(u => new AdminUserDetailDto
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email ?? string.Empty,
            Role = u.Role,
            IsActive = !u.IsDeleted,
            FullName = u.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = u.UserProfile?.PhoneNumber ?? string.Empty,
            Address = u.UserProfile?.Address ?? string.Empty,
            IdentityNumber = u.UserProfile?.IdentityNumber ?? string.Empty,
            DateOfBirth = u.UserProfile?.DateOfBirth,
            AvatarImg = u.UserProfile?.AvatarImg ?? string.Empty,
            VerificationStatus = u.UserProfile?.VerificationStatus ?? Domain.Enums.VerificationStatus.PENDING_VERIFICATION,
            AvailableBalance = u.Wallet?.AvailableBalance ?? 0m,
            LockedBalance = u.Wallet?.LockedBalance ?? 0m,
            HasPkiKeys = u.UserSecurityProfile != null,
            CreatedDate = u.CreatedDate
        }).ToList();

        return PagedResult<AdminUserDetailDto>.Create(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
