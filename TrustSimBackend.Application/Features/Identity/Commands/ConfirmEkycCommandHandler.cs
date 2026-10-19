using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý Pha 2 quy trình eKYC: Xác nhận qua Intent Key (VerificationId).
/// Hoàn toàn không nhận thông tin cá nhân từ client để phòng chống gian lận dữ liệu.
/// Toàn bộ thông tin định danh (CCCD, Họ tên, Ngày sinh, Địa chỉ) được bóc tách an toàn từ OcrRawData của Pha 1.
/// Kích hoạt cặp khóa chữ ký số PKI RSA-2048 nếu vượt qua ngưỡng kiểm định AI.
/// </summary>
public class ConfirmEkycCommandHandler : IRequestHandler<ConfirmEkycCommand, UserProfileDto>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPkiKeyService _pkiKeyService;
    private readonly AwsRekognitionSettings _settings;
    private readonly ILogger<ConfirmEkycCommandHandler> _logger;

    public ConfirmEkycCommandHandler(
        IUnitOfWork unitOfWork,
        IPkiKeyService pkiKeyService,
        IOptions<AwsRekognitionSettings> settings,
        ILogger<ConfirmEkycCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _pkiKeyService = pkiKeyService;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<UserProfileDto> Handle(ConfirmEkycCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.UserId}'.");

        if (user.UserProfile == null)
        {
            throw new NotFoundException("Hồ sơ người dùng chưa được khởi tạo.");
        }

        var verification = await _unitOfWork.IdentityVerifications.GetByIdAsync(request.VerificationId, cancellationToken);
        if (verification == null || verification.UserProfileId != user.UserProfile.Id)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ xác thực eKYC với ID '{request.VerificationId}'.");
        }

        if (verification.VerificationStatus == VerificationStatus.VERIFIED && user.UserProfile.VerificationStatus == VerificationStatus.VERIFIED)
        {
            throw new IdentityAlreadyVerifiedException();
        }

        if (verification.VerificationStatus == VerificationStatus.REJECTED)
        {
            throw new BadRequestException("Hồ sơ eKYC này đã bị từ chối do chất lượng ảnh hoặc khuôn mặt không khớp.");
        }

        // Bóc tách an toàn toàn bộ dữ liệu định danh từ OcrRawData của Pha 1 đã lưu trong cơ sở dữ liệu
        string? identityNumber = null;
        string? fullName = null;
        string? dobStr = null;
        string? address = null;

        if (!string.IsNullOrWhiteSpace(verification.OcrRawData))
        {
            try
            {
                using var doc = JsonDocument.Parse(verification.OcrRawData);
                if (doc.RootElement.TryGetProperty("extractedInfo", out var extractedInfo))
                {
                    if (extractedInfo.TryGetProperty("identityNumber", out var idProp))
                        identityNumber = idProp.GetString()?.Trim();

                    if (extractedInfo.TryGetProperty("fullName", out var nameProp))
                        fullName = nameProp.GetString()?.Trim();

                    if (extractedInfo.TryGetProperty("dateOfBirth", out var dobProp))
                        dobStr = dobProp.GetString()?.Trim();

                    if (extractedInfo.TryGetProperty("placeOfResidence", out var resProp))
                        address = resProp.GetString()?.Trim();

                    if (string.IsNullOrWhiteSpace(address) && extractedInfo.TryGetProperty("placeOfOrigin", out var originProp))
                        address = originProp.GetString()?.Trim();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "[eKYC Pha 2] Không thể parse OcrRawData của Verification {VerificationId}", verification.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(identityNumber))
        {
            throw new BadRequestException("Không tìm thấy thông tin số CMND/CCCD hợp lệ từ kết quả phân tích ảnh CCCD.");
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BadRequestException("Không tìm thấy thông tin Họ và tên hợp lệ từ kết quả phân tích ảnh CCCD.");
        }

        var cleanIdentity = identityNumber.Trim();

        // Kiểm tra tính duy nhất của số CCCD (chống trùng lặp trên toàn hệ thống)
        var isDuplicate = await _unitOfWork.Users.ExistsByIdentityNumberAsync(cleanIdentity, user.Id, cancellationToken);
        if (isDuplicate)
        {
            throw new DuplicateIdentityNumberException(cleanIdentity);
        }

        // Cập nhật thông tin định danh vào UserProfile hoàn toàn từ dữ liệu OCR gốc
        user.UserProfile.IdentityNumber = cleanIdentity;
        user.UserProfile.FullName = fullName;

        if (!string.IsNullOrWhiteSpace(dobStr) &&
            DateTime.TryParseExact(dobStr.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, DateTimeStyles.None, out var dob))
        {
            user.UserProfile.DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
        }

        if (!string.IsNullOrWhiteSpace(address))
        {
            var labelPattern = @"(?i)\b(?:N[oơòóỏõọôồốổỗộơờớởỡợ]?i\s+th[uư][rw]?[oơôòóỏõọồốổỗộờớởỡợ]?ng\s+tr[uùúủũụưừứửữự]?|Place\s+of\s+residence|Residence|N[oơòóỏõọôồốổỗộơờớởỡợ]?i\s+c[uưùúủũụưừứửữự]?\s+tr[uùúủũụưừứửữự]?|Qu[eêèéẻẽẹêềếểễệ]?\s+qu[aáàảãạâầấẩẫậăằắẳẵặ]?n|Place\s+of\s+origin|N[oơòóỏõọôồốổỗộơờớởỡợ]?i\s+sinh|Place\s+of\s+birth|ĐKHK\s+th[uư][rw]?[oơôòóỏõọồốổỗộờớởỡợ]?ng\s+tr[uùúủũụưừứửữự]?|H[oộòóỏõọôồốổỗộơờớởỡợ]?\s+kh[aẩàáảãạâầấẩẫậăằắẳẵặ]?u\s+th[uư][rw]?[oơôòóỏõọồốổỗộờớởỡợ]?ng\s+tr[uùúủũụưừứửữự]?)\b\s*[/:\-]*\s*";
            var cleanedAddress = Regex.Replace(address, labelPattern, " ");
            cleanedAddress = Regex.Replace(cleanedAddress, @"^(?:\s*(?:Noi\s+thu[rw]?ong\s+tru|Place\s+of\s+residence|Residence|Noi\s+cu\s+tru|Que\s+quan|Place\s+of\s+origin|Noi\s+sinh|DKHK\s+thu[rw]?ong\s+tru)\s*[/:\-]*\s*)+", "", RegexOptions.IgnoreCase);
            cleanedAddress = Regex.Replace(cleanedAddress, @"^[\s/:\-,]+", "");
            cleanedAddress = Regex.Replace(cleanedAddress, @"\s+", " ").Trim(',', ' ', ':', '-', '/');

            user.UserProfile.Address = string.IsNullOrWhiteSpace(cleanedAddress) ? address.Trim() : cleanedAddress;
        }

        var now = DateTime.UtcNow;
        user.UserProfile.UpdatedDate = now;

        // Đánh giá ngưỡng điểm tương đồng khuôn mặt để kích hoạt VERIFIED hoặc PENDING_VERIFICATION
        if (verification.FaceSimilarityScore >= _settings.AutoApproveSimilarityThreshold)
        {
            verification.VerificationStatus = VerificationStatus.VERIFIED;
            verification.VerifiedAt = now;
            verification.UpdatedDate = now;

            user.UserProfile.VerificationStatus = VerificationStatus.VERIFIED;

            // Tự động khởi tạo hoặc cập nhật cặp khóa ký số PKI RSA-2048
            var keyPair = _pkiKeyService.GenerateKeyPair();
            if (user.UserSecurityProfile == null)
            {
                user.UserSecurityProfile = new UserSecurityProfile
                {
                    UserAccountId = user.Id,
                    PublicKey = keyPair.PublicKeyPem,
                    EncryptedPrivateKey = keyPair.EncryptedPrivateKey,
                    KeyHash = keyPair.KeyHash,
                    KeyGeneratedAt = now,
                    UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE,
                    IsActive = true,
                    CreatedDate = now
                };
            }
            else
            {
                user.UserSecurityProfile.PublicKey = keyPair.PublicKeyPem;
                user.UserSecurityProfile.EncryptedPrivateKey = keyPair.EncryptedPrivateKey;
                user.UserSecurityProfile.KeyHash = keyPair.KeyHash;
                user.UserSecurityProfile.KeyGeneratedAt = now;
                user.UserSecurityProfile.UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE;
                user.UserSecurityProfile.UpdatedDate = now;
            }
        }
        else
        {
            verification.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";
            verification.UpdatedDate = now;

            user.UserProfile.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
        }

        user.UpdatedDate = now;

        _unitOfWork.Users.Update(user);
        _unitOfWork.IdentityVerifications.Update(verification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[eKYC Pha 2] Người dùng {UserId} xác nhận thành công thông tin eKYC {VerificationId}. Trạng thái={Status}",
            user.Id, verification.Id, user.UserProfile.VerificationStatus);

        return new UserProfileDto
        {
            UserId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.UserProfile.FullName,
            PhoneNumber = user.UserProfile.PhoneNumber,
            Address = user.UserProfile.Address,
            AvatarImg = user.UserProfile.AvatarImg,
            IdentityNumber = user.UserProfile.IdentityNumber,
            DateOfBirth = user.UserProfile.DateOfBirth,
            VerificationStatus = user.UserProfile.VerificationStatus,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            WalletBalance = user.Wallet?.AvailableBalance ?? 0
        };
    }
}
