using MediatR;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý phê duyệt hoặc từ chối hồ sơ KYC từ Admin và tự động kích hoạt cặp khóa ký số PKI.
/// </summary>
public class ReviewKycCommandHandler : IRequestHandler<ReviewKycCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPkiKeyService _pkiKeyService;

    public ReviewKycCommandHandler(
        IUnitOfWork unitOfWork,
        IPkiKeyService pkiKeyService)
    {
        _unitOfWork = unitOfWork;
        _pkiKeyService = pkiKeyService;
    }

    public async Task<bool> Handle(ReviewKycCommand request, CancellationToken cancellationToken)
    {
        var verification = await _unitOfWork.IdentityVerifications.GetByIdAsync(request.VerificationId, cancellationToken);
        if (verification == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ KYC với ID '{request.VerificationId}'.");
        }

        verification.VerificationStatus = request.Status;
        verification.RejectionReason = request.RejectionReason?.Trim();
        verification.VerifiedAt = DateTime.UtcNow;
        verification.VerifiedByUserId = request.AdminUserId;
        verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";
        verification.UpdatedDate = DateTime.UtcNow;

        if (verification.UserProfile != null)
        {
            verification.UserProfile.VerificationStatus = request.Status;
            verification.UserProfile.UpdatedDate = DateTime.UtcNow;

            // Nếu Admin phê duyệt, tự động đồng bộ thông tin CCCD vào UserProfile và kích hoạt cặp khóa ký số PKI cho người dùng
            if (request.Status == VerificationStatus.VERIFIED)
            {
                // 1. Đồng bộ thông tin bóc tách từ ảnh CCCD (OcrRawData) vào UserProfile nếu có
                if (!string.IsNullOrWhiteSpace(verification.OcrRawData))
                {
                    try
                    {
                        using var doc = global::System.Text.Json.JsonDocument.Parse(verification.OcrRawData);
                        if (doc.RootElement.TryGetProperty("extractedInfo", out var info))
                        {
                            if (string.IsNullOrWhiteSpace(verification.UserProfile.FullName) && info.TryGetProperty("fullName", out var nameProp) && nameProp.GetString() is { } name && !string.IsNullOrWhiteSpace(name))
                            {
                                verification.UserProfile.FullName = name.Trim();
                            }

                            if (string.IsNullOrWhiteSpace(verification.UserProfile.IdentityNumber) && info.TryGetProperty("identityNumber", out var idProp) && idProp.GetString() is { } id && !string.IsNullOrWhiteSpace(id))
                            {
                                verification.UserProfile.IdentityNumber = id.Trim();
                            }

                            if (!verification.UserProfile.DateOfBirth.HasValue && info.TryGetProperty("dateOfBirth", out var dobProp) && dobProp.GetString() is { } dobStr && !string.IsNullOrWhiteSpace(dobStr))
                            {
                                if (DateTime.TryParseExact(dobStr.Trim(), new[] { "dd/MM/yyyy", "yyyy-MM-dd", "d/M/yyyy" }, 
                                    global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.None, out var parsedDob))
                                {
                                    verification.UserProfile.DateOfBirth = DateTime.SpecifyKind(parsedDob, DateTimeKind.Utc);
                                }
                            }

                            if (string.IsNullOrWhiteSpace(verification.UserProfile.Address))
                            {
                                if (info.TryGetProperty("placeOfResidence", out var resProp) && resProp.GetString() is { } res && !string.IsNullOrWhiteSpace(res))
                                {
                                    verification.UserProfile.Address = res.Trim();
                                }
                                else if (info.TryGetProperty("address", out var addrProp) && addrProp.GetString() is { } addr && !string.IsNullOrWhiteSpace(addr))
                                {
                                    verification.UserProfile.Address = addr.Trim();
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Bỏ qua lỗi parse JSON nếu dữ liệu lưu thô ở phiên bản cũ không chuẩn JSON
                    }
                }


                var user = await _unitOfWork.Users.GetByIdAsync(verification.UserProfile.UserAccountId, cancellationToken);
                if (user != null)
                {
                    if (user.UserSecurityProfile == null)
                    {
                        var keyPair = _pkiKeyService.GenerateKeyPair();
                        user.UserSecurityProfile = new UserSecurityProfile
                        {
                            UserAccountId = user.Id,
                            PublicKey = keyPair.PublicKeyPem,
                            EncryptedPrivateKey = keyPair.EncryptedPrivateKey,
                            KeyHash = keyPair.KeyHash,
                            KeyGeneratedAt = DateTime.UtcNow,
                            UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE,
                            IsActive = true,
                            CreatedDate = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        user.UserSecurityProfile.UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE;
                        user.UserSecurityProfile.UpdatedDate = DateTime.UtcNow;
                    }

                    user.UpdatedDate = DateTime.UtcNow;
                    _unitOfWork.Users.Update(user);
                }
            }
        }

        _unitOfWork.IdentityVerifications.Update(verification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
