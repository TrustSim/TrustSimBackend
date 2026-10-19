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
    private readonly IAwsRekognitionService _rekognitionService;
    private readonly ICloudStorageService _storageService;
    private readonly IPkiKeyService _pkiKeyService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AwsRekognitionSettings _settings;
    private readonly ILogger<SubmitEkycVerificationCommandHandler> _logger;

    public SubmitEkycVerificationCommandHandler(
        IAwsRekognitionService rekognitionService,
        ICloudStorageService storageService,
        IPkiKeyService pkiKeyService,
        IUnitOfWork unitOfWork,
        IOptions<AwsRekognitionSettings> settings,
        ILogger<SubmitEkycVerificationCommandHandler> logger)
    {
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
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken)
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

        byte[]? backBytes = null;
        if (request.BackCardImage != null)
        {
            using var backMs = new MemoryStream();
            await request.BackCardImage.CopyToAsync(backMs, cancellationToken);
            backBytes = backMs.ToArray();
        }

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
        var isDuplicate = await _unitOfWork.Users.ExistsByIdentityNumberAsync(
            ocrResult.IdentityNumber, user.Id, cancellationToken);

        if (isDuplicate)
        {
            throw new DuplicateIdentityNumberException(ocrResult.IdentityNumber);
        }

        // 8. Gọi AWS Rekognition CompareFaces (So khớp CCCD vs Selfie)
        var faceMatch = await _rekognitionService.CompareFacesAsync(
            frontBytes, selfieBytes, _settings.ManualReviewSimilarityThreshold, cancellationToken);

        // 9. Tối ưu: Upload song song (Parallel Task.WhenAll) cả 3 ảnh lên Supabase Storage qua connection pool
        var frontExt = Path.GetExtension(request.FrontCardImage.FileName);
        if (string.IsNullOrWhiteSpace(frontExt)) frontExt = ".jpg";
        var frontUploadTask = _storageService.UploadImageBytesAsync(
            frontBytes, $"{Guid.NewGuid():N}{frontExt}", "kyc/front", request.FrontCardImage.ContentType ?? "image/jpeg", cancellationToken);

        var selfieExt = Path.GetExtension(request.SelfieImage.FileName);
        if (string.IsNullOrWhiteSpace(selfieExt)) selfieExt = ".jpg";
        var selfieUploadTask = _storageService.UploadImageBytesAsync(
            selfieBytes, $"{Guid.NewGuid():N}{selfieExt}", "kyc/selfie", request.SelfieImage.ContentType ?? "image/jpeg", cancellationToken);

        Task<string> backUploadTask;
        if (backBytes != null && request.BackCardImage != null)
        {
            var backExt = Path.GetExtension(request.BackCardImage.FileName);
            if (string.IsNullOrWhiteSpace(backExt)) backExt = ".jpg";
            backUploadTask = _storageService.UploadImageBytesAsync(
                backBytes, $"{Guid.NewGuid():N}{backExt}", "kyc/back", request.BackCardImage.ContentType ?? "image/jpeg", cancellationToken);
        }
        else
        {
            backUploadTask = Task.FromResult(string.Empty);
        }

        await Task.WhenAll(frontUploadTask, selfieUploadTask, backUploadTask);

        var frontUrl = await frontUploadTask;
        var selfieUrl = await selfieUploadTask;
        var backUrl = await backUploadTask;

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
            // Đạt chuẩn xuất sắc qua AI -> Chờ người dùng xác nhận ở API 2 để cập nhật vào hồ sơ
            isAutoApproved = true;
            verification.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            verification.VerificationMethod = "AUTOMATED_AWS_REKOGNITION";
            returnMessage = "Phân tích AI eKYC thành công! Vui lòng kiểm tra lại thông tin và bấm Xác nhận để hoàn tất cập nhật hồ sơ.";
        }
        else if (faceMatch.SimilarityScore >= _settings.ManualReviewSimilarityThreshold)
        {
            // Điểm cận biên -> Chuyển sang chờ người dùng xác nhận & Admin duyệt
            verification.VerificationStatus = VerificationStatus.PENDING_VERIFICATION;
            verification.VerificationMethod = "MANUAL_ADMIN_REVIEW";
            returnMessage = "Ảnh eKYC đã được tiếp nhận. Vui lòng kiểm tra lại thông tin và bấm Xác nhận để gửi chuyên viên đối soát.";
        }
        else
        {
            // Dưới ngưỡng -> TỪ CHỐI (REJECTED)
            verification.VerificationStatus = VerificationStatus.REJECTED;
            verification.VerificationMethod = "AUTOMATED_AWS_REKOGNITION";
            verification.RejectionReason = $"Độ tương đồng khuôn mặt không đạt yêu cầu ({faceMatch.SimilarityScore:F1}% < {_settings.ManualReviewSimilarityThreshold:F0}%).";
            verification.VerifiedAt = DateTime.UtcNow;

            await _unitOfWork.IdentityVerifications.AddAsync(verification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Dọn dẹp tệp ảnh tạm thời trên web server
            await CleanupTempUploadsAsync(frontUrl, backUrl, selfieUrl);

            throw new KycFaceMismatchException(faceMatch.SimilarityScore);
        }

        await _unitOfWork.IdentityVerifications.AddAsync(verification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("[eKYC Pha 1] Phân tích AI hoàn tất cho User {UserId}: VerificationId={VerificationId}, Score={Sim}%",
            user.Id, verification.Id, faceMatch.SimilarityScore);

        return new EkycVerificationResultDto
        {
            VerificationId = verification.Id,
            UserProfileId = user.UserProfile.Id,
            VerificationStatus = verification.VerificationStatus,
            IsAutoApproved = isAutoApproved,
            FaceSimilarityScore = faceMatch.SimilarityScore,
            ExtractedFullName = ocrResult.FullName ?? string.Empty,
            ExtractedIdentityNumber = ocrResult.IdentityNumber ?? string.Empty,
            ExtractedDateOfBirth = ocrResult.DateOfBirth,
            ExtractedAddress = ocrResult.Address ?? string.Empty,
            FrontCardUrl = frontUrl,
            BackCardUrl = backUrl,
            SelfieUrl = selfieUrl,
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
