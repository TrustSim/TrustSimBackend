using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Features.Identity.DTOs.Requests;
using TrustSimBackend.Application.Features.Identity.DTOs.Responses;
using TrustSimBackend.Domain.Entities.Identity;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Features.Identity.Commands;

/// <summary>
/// Handler xử lý quy trình eKYC tự động tích hợp AWS Rekognition (OCR + Face Detection + Face Matching).
/// </summary>
public class SubmitEkycVerificationCommandHandler : IRequestHandler<SubmitEkycVerificationCommand, EkycVerificationResultDto>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IIdentityVerificationRepository _kycRepository;
    private readonly IAwsRekognitionService _rekognitionService;
    private readonly ICloudStorageService _storageService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AwsRekognitionSettings _settings;
    private readonly ILogger<SubmitEkycVerificationCommandHandler> _logger;

    public SubmitEkycVerificationCommandHandler(
        IUserAccountRepository userAccountRepository,
        IIdentityVerificationRepository kycRepository,
        IAwsRekognitionService rekognitionService,
        ICloudStorageService storageService,
        IUnitOfWork unitOfWork,
        IOptions<AwsRekognitionSettings> settings,
        ILogger<SubmitEkycVerificationCommandHandler> logger)
    {
        _userAccountRepository = userAccountRepository;
        _kycRepository = kycRepository;
        _rekognitionService = rekognitionService;
        _storageService = storageService;
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

        // 5. Kiểm tra tính duy nhất của số CCCD trên toàn hệ thống (chống clone nick)
        var isDuplicate = await _userAccountRepository.ExistsByIdentityNumberAsync(
            ocrResult.IdentityNumber, user.Id, cancellationToken);

        if (isDuplicate)
        {
            throw new DuplicateIdentityNumberException(ocrResult.IdentityNumber);
        }

        // 6. Gọi AWS Rekognition CompareFaces (So khớp CCCD vs Selfie)
        var faceMatch = await _rekognitionService.CompareFacesAsync(
            frontBytes, selfieBytes, _settings.ManualReviewSimilarityThreshold, cancellationToken);

        // 7. Upload ảnh lên Cloud Storage để lưu trữ bằng chứng
        var frontUrl = await _storageService.UploadImageAsync(request.FrontCardImage, "kyc/front", cancellationToken);
        var selfieUrl = await _storageService.UploadImageAsync(request.SelfieImage, "kyc/selfie", cancellationToken);
        string backUrl = string.Empty;
        if (request.BackCardImage != null)
        {
            backUrl = await _storageService.UploadImageAsync(request.BackCardImage, "kyc/back", cancellationToken);
        }

        // 8. Động cơ Phân luồng Quyết định Hybrid
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

            user.UserProfile.VerificationStatus = VerificationStatus.VERIFIED;
            user.UserProfile.IdentityNumber = ocrResult.IdentityNumber;
            if (!string.IsNullOrEmpty(ocrResult.FullName)) user.UserProfile.FullName = ocrResult.FullName;
            if (ocrResult.DateOfBirth.HasValue) user.UserProfile.DateOfBirth = ocrResult.DateOfBirth.Value;
            if (!string.IsNullOrEmpty(ocrResult.Address)) user.UserProfile.Address = ocrResult.Address;
            user.UserProfile.AvatarImg = selfieUrl;

            returnMessage = "Xác thực danh tính eKYC tự động thành công qua AI!";
        }
        else if (faceMatch.SimilarityScore >= _settings.ManualReviewSimilarityThreshold)
        {
            // TH2: Điểm cận biên -> CHUYỂN ADMIN DUYỆT (PENDING_VERIFICATION)
            verification.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";

            user.UserProfile.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            user.UserProfile.IdentityNumber = ocrResult.IdentityNumber;
            if (!string.IsNullOrEmpty(ocrResult.FullName)) user.UserProfile.FullName = ocrResult.FullName;
            if (ocrResult.DateOfBirth.HasValue) user.UserProfile.DateOfBirth = ocrResult.DateOfBirth.Value;
            user.UserProfile.AvatarImg = selfieUrl;

            returnMessage = "Hồ sơ eKYC của bạn đã được tiếp nhận và đang chờ chuyên viên đối soát trong vòng 24h.";
        }
        else
        {
            // TH3: Dưới 70% -> TỪ CHỐI (REJECTED)
            verification.VerificationStatus = VerificationStatus.REJECTED;
            verification.VerificationMethod = "AUTOMATED_AWS_REKOGNITION";
            verification.RejectionReason = $"Độ tương đồng khuôn mặt không đạt yêu cầu ({faceMatch.SimilarityScore:F1}% < {_settings.ManualReviewSimilarityThreshold:F0}%).";
            verification.VerifiedAt = DateTime.UtcNow;

            user.UserProfile.VerificationStatus = VerificationStatus.REJECTED;

            _userAccountRepository.Update(user);
            await _kycRepository.AddAsync(verification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new KycFaceMismatchException(faceMatch.SimilarityScore);
        }

        _userAccountRepository.Update(user);
        await _kycRepository.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
}
