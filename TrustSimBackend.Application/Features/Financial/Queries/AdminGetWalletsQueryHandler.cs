using MediatR;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Financial.DTOs.Requests;
using TrustSimBackend.Application.Features.Financial.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Financial.Queries;

public class AdminGetWalletsQueryHandler : IRequestHandler<AdminGetWalletsQuery, PagedResult<AdminWalletDetailDto>>
{
    private readonly IUnitOfWork _unitOfWork;

    public AdminGetWalletsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<AdminWalletDetailDto>> Handle(AdminGetWalletsQuery request, CancellationToken cancellationToken)
    {
        var (wallets, totalCount) = await _unitOfWork.Wallets.GetPagedAsync(
            request.Filter,
            cancellationToken);

        var dtos = wallets.Select(w => new AdminWalletDetailDto
        {
            Id = w.Id,
            UserAccountId = w.UserAccountId,
            UserName = w.UserAccount?.UserName ?? string.Empty,
            Email = w.UserAccount?.UserEmail ?? string.Empty,
            FullName = w.UserAccount?.UserProfile?.FullName ?? string.Empty,
            AvailableBalance = w.AvailableBalance,
            LockedBalance = w.LockedBalance,
            WalletStatus = w.WalletStatus,
            CreatedDate = w.CreatedDate
        }).ToList();

        return new PagedResult<AdminWalletDetailDto>(dtos, totalCount, request.Filter.PageIndex, request.Filter.PageSize);
    }
}
