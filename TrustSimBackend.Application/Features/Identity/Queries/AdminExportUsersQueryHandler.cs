using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

public class AdminExportUsersQueryHandler : IRequestHandler<AdminExportUsersQuery, byte[]>
{
    private readonly IUserAccountRepository _userRepository;
    private readonly IExcelService _excelService;

    public AdminExportUsersQueryHandler(IUserAccountRepository userRepository, IExcelService excelService)
    {
        _userRepository = userRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportUsersQuery request, CancellationToken cancellationToken)
    {
        var (users, _) = await _userRepository.GetPagedAsync(
            request.Keyword,
            request.Role,
            request.IsActive,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

        var rows = users.Select(u => new UserExcelExportRow
        {
            UserName = u.UserName,
            Email = u.UserEmail,
            FullName = u.UserProfile?.FullName ?? string.Empty,
            PhoneNumber = u.UserProfile?.PhoneNumber ?? string.Empty,
            IdentityNumber = u.UserProfile?.IdentityNumber ?? string.Empty,
            Role = u.Role.ToString(),
            VerificationStatus = u.UserProfile?.VerificationStatus.ToString() ?? "UNVERIFIED",
            IsActive = u.IsDeleted ? "Đã khóa" : "Hoạt động",
            AvailableBalance = u.Wallet?.AvailableBalance ?? 0m,
            LockedBalance = u.Wallet?.LockedBalance ?? 0m,
            CreatedDate = u.CreatedDate.ToString("yyyy-MM-dd HH:mm:ss")
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "Users", cancellationToken: cancellationToken);
    }
}
