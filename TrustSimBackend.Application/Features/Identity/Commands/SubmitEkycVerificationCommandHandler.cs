using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Helpers;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý quy trình eKYC tự động tích hợp AWS Rekognition (OCR + Face Detection + Face Matching)
/// kết hợp đối soát thông tin hồ sơ, tự động khởi tạo cặp khóa ký số PKI RSA-2048 và dọn dẹp ảnh tạm thời.
/// </summary>
public class SubmitEkycVerificationCommandHandler : IRequestHandler<SubmitEkycVerificationCommand, EkycVerificationResultDto>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IAwsRekognitionService _rekognitionService;
    private readonly ICloudStorageService _storageService;
    private readonly IPkiKeyService _pkiKeyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AwsRekognitionSettings _settings;
    private readonly ILogger<SubmitEkycVerificationCommandHandler> _logger;

    public SubmitEkycVerificationCommandHandler(
        IUserAccountRepository userAccountRepository,
        IIdentityVerificationRepository kycRepository,
        IAwsRekognitionService rekognitionService,
        ICloudStorageService storageService,
        IPkiKeyService pkiKeyService,
        IUnitOfWork unitOfWork,
        IOptions<AwsRekognitionSettings> settings,
        ILogger<SubmitEkycVerificationCommandHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _kycRepository = kycRepository;
        _rekognitionService = rekognitionService;
        _storageService = storageService;
        _pkiKeyService = pkiKeyService;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<EkycVerificationResultDto> Handle(SubmitEkycVerificationCommand request, CancellationToken cancellationToken)
    {
        // 1. Kiểm tra tài khoản & trạng thái hiện tại
        var user = await _userAccountRepository.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy tài khoản người dùng với ID '{request.UserId}'.");

        if (user.UserProfile == null)
        {
            throw new NotFoundException("Hồ sơ người dùng chưa được khởi tạo.");
        }

        if (user.UserProfile.VerificationStatus == VerificationStatus.VERIFIED)
        {
            throw new IdentityAlreadyVerifiedException();
        }

        // 2. Chuyển đổi File sang byte[] để phân tích AI
        using var frontMs = new MemoryStream();
        await request.FrontCardImage.CopyToAsync(frontMs, cancellationToken);
        var frontBytes = frontMs.ToArray();

        using var selfieMs = new MemoryStream();
        await request.SelfieImage.CopyToAsync(selfieMs, cancellationToken);
        var selfieBytes = selfieMs.ToArray();

        // 3. Gọi AWS Rekognition DetectFaces để kiểm định ảnh Selfie
        var selfieValidation = await _rekognitionService.ValidateSelfieFaceAsync(selfieBytes, cancellationToken);
        if (!selfieValidation.IsValid)
        {
            throw new KycImageQualityException(selfieValidation.ErrorMessage ?? "Ảnh selfie không đạt yêu cầu chất lượng.");
        }

        // 4. Gọi AWS Rekognition DetectText để OCR mặt trước thẻ căn cước
        var ocrResult = await _rekognitionService.ExtractDocumentTextAsync(frontBytes, cancellationToken);
        if (!ocrResult.IsSuccess || string.IsNullOrWhiteSpace(ocrResult.IdentityNumber))
        {
            throw new InvalidIdentityDocumentException("Không thể đọc được số CMND/CCCD từ hình ảnh cung cấp. Vui lòng chụp rõ nét hơn.");
        }

        // 5. Đối soát số CCCD với UserProfile nếu hồ sơ đã có số CCCD
        if (!string.IsNullOrWhiteSpace(user.UserProfile.IdentityNumber) &&
            !IdentityMatchingHelper.IsIdentityNumberMatch(ocrResult.IdentityNumber, user.UserProfile.IdentityNumber))
        {
            throw new IdentityNumberMismatchException(ocrResult.IdentityNumber, user.UserProfile.IdentityNumber);
        }

        // 6. Đối soát Họ và tên với UserProfile nếu cả 2 bên đều có thông tin
        if (!string.IsNullOrWhiteSpace(user.UserProfile.FullName) &&
            !string.IsNullOrWhiteSpace(ocrResult.FullName) &&
            !IdentityMatchingHelper.IsFullNameMatch(ocrResult.FullName, user.UserProfile.FullName))
        {
            throw new IdentityNameMismatchException(ocrResult.FullName, user.UserProfile.FullName);
        }

        // 7. Kiểm tra tính duy nhất của số CCCD trên toàn hệ thống (chống clone nick)
        var isDuplicate = await _userAccountRepository.ExistsByIdentityNumberAsync(
            ocrResult.IdentityNumber, user.Id, cancellationToken);

        if (isDuplicate)
        {
            throw new DuplicateIdentityNumberException(ocrResult.IdentityNumber);
        }

        // 8. Gọi AWS Rekognition CompareFaces (So khớp CCCD vs Selfie)
        var faceMatch = await _rekognitionService.CompareFacesAsync(
            frontBytes, selfieBytes, _settings.ManualReviewSimilarityThreshold, cancellationToken);

        // 9. Upload ảnh lên Cloud Storage để lưu trữ bằng chứng
        var frontUrl = await _storageService.UploadImageAsync(request.FrontCardImage, "kyc/front", cancellationToken);
        var selfieUrl = await _storageService.UploadImageAsync(request.SelfieImage, "kyc/selfie", cancellationToken);
        string backUrl = string.Empty;
        if (request.BackCardImage != null)
        {
            backUrl = await _storageService.UploadImageAsync(request.BackCardImage, "kyc/back", cancellationToken);
        }

        // 10. Động cơ Phân luồng Quyết định Hybrid
        var verification = new IdentityVerification
        {
            Id = Guid.NewGuid(),
            UserProfileId = user.UserProfile.Id,
            FrontIdCardUrl = frontUrl,
            BackIdCardUrl = backUrl,
            FaceSelfieUrl = selfieUrl,
            FaceSimilarityScore = faceMatch.SimilarityScore,
            OcrRawData = ocrResult.RawJson,
            SubmittedAt = DateTime.UtcNow
        };

        bool isAutoApproved = false;
        string returnMessage;

        if (faceMatch.IsMatched && faceMatch.SimilarityScore >= _settings.AutoApproveSimilarityThreshold)
        {
            // TH1: Đạt chuẩn xuất sắc -> TỰ ĐỘNG DUYỆT (VERIFIED)
            isAutoApproved = true;
            verification.VerificationStatus = VerificationStatus.VERIFIED;
            verification.VerificationMethod = "AUTOMATED_AWS_REKOGNITION";
            verification.VerifiedAt = DateTime.UtcNow;

            // Cập nhật UserProfile
            user.UserProfile.VerificationStatus = VerificationStatus.VERIFIED;
            user.UserProfile.IdentityNumber = ocrResult.IdentityNumber;
            if (!string.IsNullOrEmpty(ocrResult.FullName))
            {
                user.UserProfile.FullName = ocrResult.FullName;
            }
            if (ocrResult.DateOfBirth.HasValue)
            {
                user.UserProfile.DateOfBirth = ocrResult.DateOfBirth.Value;
            }
            if (!string.IsNullOrEmpty(ocrResult.Address))
            {
                user.UserProfile.Address = ocrResult.Address;
            }
            user.UserProfile.AvatarImg = selfieUrl;
            user.UserProfile.UpdatedDate = DateTime.UtcNow;

            // Khởi tạo/Kích hoạt cặp khóa ký số PKI RSA-2048 trong UserSecurityProfile
            var keyPair = _pkiKeyService.GenerateKeyPair();
            if (user.UserSecurityProfile == null)
            {
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
                user.UserSecurityProfile.PublicKey = keyPair.PublicKeyPem;
                user.UserSecurityProfile.EncryptedPrivateKey = keyPair.EncryptedPrivateKey;
                user.UserSecurityProfile.KeyHash = keyPair.KeyHash;
                user.UserSecurityProfile.KeyGeneratedAt = DateTime.UtcNow;
                user.UserSecurityProfile.UserSecurityProfileStatus = UserSecurityProfileStatus.ACTIVE;
                user.UserSecurityProfile.UpdatedDate = DateTime.UtcNow;
            }

            // Cập nhật UserAccount
            user.UpdatedDate = DateTime.UtcNow;

            returnMessage = "Xác thực danh tính eKYC tự động thành công qua AI! Cặp khóa ký số PKI đã được kích hoạt.";
        }
        else if (faceMatch.SimilarityScore >= _settings.ManualReviewSimilarityThreshold)
        {
            // TH2: Điểm cận biên -> CHUYỂN ADMIN DUYỆT (PENDING_VERIFICATION)
            verification.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";

            user.UserProfile.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            user.UserProfile.IdentityNumber = ocrResult.IdentityNumber;
            if (!string.IsNullOrEmpty(ocrResult.FullName))
            {
                user.UserProfile.FullName = ocrResult.FullName;
            }
            if (ocrResult.DateOfBirth.HasValue)
            {
                user.UserProfile.DateOfBirth = ocrResult.DateOfBirth.Value;
            }
            if (!string.IsNullOrEmpty(ocrResult.Address))
            {
                user.UserProfile.Address = ocrResult.Address;
            }
            user.UserProfile.AvatarImg = selfieUrl;
            user.UserProfile.UpdatedDate = DateTime.UtcNow;

            user.UpdatedDate = DateTime.UtcNow;

            returnMessage = "Hồ sơ eKYC của bạn đã được tiếp nhận và đang chờ chuyên viên đối soát trong vòng 24h.";
        }
        else
        {
            // TH3: Dưới ngưỡng -> TỪ CHỐI (REJECTED)
            verification.VerificationStatus = VerificationStatus.REJECTED;
            verification.VerificationMethod = "AUTOMATED_AWS_REKOGNITION";
            verification.RejectionReason = $"Độ tương đồng khuôn mặt không đạt yêu cầu ({faceMatch.SimilarityScore:F1}% < {_settings.ManualReviewSimilarityThreshold:F0}%).";
            verification.VerifiedAt = DateTime.UtcNow;

            user.UserProfile.VerificationStatus = VerificationStatus.REJECTED;
            user.UserProfile.UpdatedDate = DateTime.UtcNow;
            user.UpdatedDate = DateTime.UtcNow;

            _userAccountRepository.Update(user);
            await _kycRepository.AddAsync(verification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Dọn dẹp tệp ảnh tạm thời trên web server
            await CleanupTempUploadsAsync(frontUrl, backUrl, selfieUrl);

            throw new KycFaceMismatchException(faceMatch.SimilarityScore);
        }

        _userAccountRepository.Update(user);
        await _kycRepository.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 11. Tự động dọn dẹp các tệp ảnh tạm trên web server (wwwroot/uploads) sau khi lưu DB thành công
        await CleanupTempUploadsAsync(frontUrl, backUrl, selfieUrl);

        _logger.LogInformation("[eKYC] Xử lý hoàn tất cho User {UserId}: Trạng thái={Status}, AutoApproved={Auto}, Similarity={Sim}%",
            user.Id, verification.VerificationStatus, isAutoApproved, faceMatch.SimilarityScore);

        return new EkycVerificationResultDto
        {
            VerificationId = verification.Id,
            UserProfileId = user.UserProfile.Id,
            VerificationStatus = verification.VerificationStatus,
            IsAutoApproved = isAutoApproved,
            FaceSimilarityScore = faceMatch.SimilarityScore,
            ExtractedFullName = user.UserProfile.FullName,
            ExtractedIdentityNumber = user.UserProfile.IdentityNumber,
            ExtractedDateOfBirth = user.UserProfile.DateOfBirth,
            Message = returnMessage,
            SubmittedAt = verification.SubmittedAt
        };
    }

    private async Task CleanupTempUploadsAsync(params string[] fileUrls)
    {
        foreach (var url in fileUrls)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    await _storageService.DeleteFileAsync(url, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[eKYC] Không thể xóa tệp ảnh tạm thời: {Url}", url);
                }
            }
        }
    }
}
