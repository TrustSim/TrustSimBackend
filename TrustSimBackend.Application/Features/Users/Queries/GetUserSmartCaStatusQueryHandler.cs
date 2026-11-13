using MediatR;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Users.DTOs;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Users.Queries;

/// <summary>
/// Handler xử lý truy vấn trạng thái chứng thư số VNPT SmartCA của người dùng sau khi hoàn tất eKYC.
/// </summary>
public class GetUserSmartCaStatusQueryHandler : IRequestHandler<GetUserSmartCaStatusQuery, UserSmartCaStatusDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISmartCaService _smartCaService;
    private readonly ILogger<GetUserSmartCaStatusQueryHandler> _logger;

    public GetUserSmartCaStatusQueryHandler(
        IUnitOfWork unitOfWork,
        ISmartCaService smartCaService,
        ILogger<GetUserSmartCaStatusQueryHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _smartCaService = smartCaService;
        _logger = logger;
    }

    public async Task<UserSmartCaStatusDto> Handle(
        GetUserSmartCaStatusQuery request,
        CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.UserId);
        }

        var citizenId = user.UserProfile?.IdentityNumber?.Trim();
        var fullName = user.UserProfile?.FullName;

        // Nếu người dùng chưa có số CCCD từ bước xác thực eKYC
        if (string.IsNullOrWhiteSpace(citizenId))
        {
            return new UserSmartCaStatusDto(
                IsEkycVerified: false,
                CitizenId: null,
                FullName: fullName,
                HasSmartCaCertificate: false,
                ActiveCertificate: null);
        }

        IReadOnlyList<SmartCaCertificateDto>? certs = null;
        try
        {
            certs = await _smartCaService.GetUserCertificatesAsync(citizenId, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "[SmartCA Status] Lỗi khi tra cứu chứng thư số cho CCCD '{CitizenId}' của người dùng {UserId}: {Message}",
                citizenId, request.UserId, ex.Message);
        }

        var activeCert = certs?.FirstOrDefault(c =>
            string.Equals(c.CertStatus, "Đang hoạt động", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.CertStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.CertStatus, "OPERATIONAL", StringComparison.OrdinalIgnoreCase));

        var hasCertificate = activeCert != null && !string.IsNullOrWhiteSpace(activeCert.SerialNumber);

        var certificateDto = hasCertificate
            ? new SmartCaActiveCertificateDto(
                SerialNumber: activeCert!.SerialNumber,
                ServiceName: activeCert.ServiceName,
                CertStatus: activeCert.CertStatus,
                ValidFrom: activeCert.ValidFrom,
                ValidTo: activeCert.ValidTo)
            : null;

        return new UserSmartCaStatusDto(
            IsEkycVerified: true,
            CitizenId: citizenId,
            FullName: fullName,
            HasSmartCaCertificate: hasCertificate,
            ActiveCertificate: certificateDto);
    }
}
