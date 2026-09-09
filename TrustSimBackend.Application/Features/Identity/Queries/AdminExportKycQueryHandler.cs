using MediatR;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Queries;

public class AdminExportKycQueryHandler : IRequestHandler<AdminExportKycQuery, byte[]>
{
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IExcelService _excelService;

    public AdminExportKycQueryHandler(IIdentityVerificationRepository kycRepository, IExcelService excelService)
    {
        _kycRepository = kycRepository;
        _excelService = excelService;
    }

    public async Task<byte[]> Handle(AdminExportKycQuery request, CancellationToken cancellationToken)
    {
        var (items, _) = await _kycRepository.GetPagedAsync(
            request.Status,
            pageIndex: 1,
            pageSize: 100000,
            cancellationToken: cancellationToken);

        var rows = items.Select(k => new
        {
            VerificationId = k.Id,
            UserName = k.UserProfile?.UserAccount?.UserName ?? string.Empty,
            FullName = k.UserProfile?.FullName ?? string.Empty,
            Status = k.VerificationStatus.ToString(),
            SubmittedAt = k.SubmittedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            VerifiedAt = k.VerifiedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "",
            RejectionReason = k.RejectionReason ?? ""
        }).ToList();

        return await _excelService.WriteExcelAsync(rows, sheetName: "KycVerifications", cancellationToken: cancellationToken);
    }
}
