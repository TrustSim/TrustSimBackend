using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ AWS Rekognition xử lý thị giác máy tính eKYC (OCR, Phát hiện khuôn mặt, So khớp khuôn mặt).
/// </summary>
public class AwsRekognitionService : IAwsRekognitionService
{
    private readonly AmazonRekognitionClient _rekognitionClient;
    private readonly AwsRekognitionSettings _settings;
    private readonly ILogger<AwsRekognitionService> _logger;

    public AwsRekognitionService(
        IOptions<AwsRekognitionSettings> options,
        ILogger<AwsRekognitionService> logger)
    {
        _settings = options.Value;
        _logger = logger;

        var regionName = string.IsNullOrWhiteSpace(_settings.Region) ? "ap-southeast-1" : _settings.Region.Trim();
        var regionEndpoint = RegionEndpoint.GetBySystemName(regionName);

        var accessKey = _settings.AccessKey?.Trim() ?? string.Empty;
        var secretKey = _settings.SecretKey?.Trim() ?? string.Empty;

        var credentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
        _rekognitionClient = new AmazonRekognitionClient(credentials, regionEndpoint);
    }

    public async Task<OcrExtractionResult> ExtractDocumentTextAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DetectTextRequest
            {
                Image = new Image { Bytes = new MemoryStream(imageBytes) }
            };

            var response = await _rekognitionClient.DetectTextAsync(request, cancellationToken);
            var lines = response.TextDetections
                .Where(t => t.Type == TextTypes.LINE && t.Confidence >= _settings.MinConfidenceThreshold)
                .Select(t => t.DetectedText.Trim())
                .ToList();

            var result = new OcrExtractionResult
            {
                IsSuccess = true,
                DetectedLines = lines,
                OverallConfidence = response.TextDetections.Count > 0 ? response.TextDetections.Average(x => x.Confidence) : 0,
                RawJson = JsonSerializer.Serialize(lines)
            };

            ParseVietnameseIdCard(lines, result);

            _logger.LogInformation("[AWS Rekognition] Trích xuất OCR thành công: CCCD={Id}, Tên={Name}, NS={Dob}",
                result.IdentityNumber, result.FullName, result.DateOfBirth);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AWS Rekognition] Lỗi khi nhận diện văn bản (DetectText)");
            return new OcrExtractionResult { IsSuccess = false, RawJson = ex.Message };
        }
    }

    public async Task<FaceDetectionResult> ValidateSelfieFaceAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new DetectFacesRequest
            {
                Image = new Image { Bytes = new MemoryStream(imageBytes) },
                Attributes = new List<string> { "ALL" }
            };

            var response = await _rekognitionClient.DetectFacesAsync(request, cancellationToken);

            if (response.FaceDetails.Count == 0)
            {
                return new FaceDetectionResult
                {
                    IsValid = false,
                    FaceCount = 0,
                    ErrorMessage = "Không phát hiện khuôn mặt nào trong ảnh chân dung selfie."
                };
            }

            if (response.FaceDetails.Count > 1)
            {
                return new FaceDetectionResult
                {
                    IsValid = false,
                    FaceCount = response.FaceDetails.Count,
                    ErrorMessage = "Phát hiện nhiều hơn 1 khuôn mặt trong ảnh selfie. Vui lòng chỉ chụp 1 người duy nhất."
                };
            }

            var face = response.FaceDetails[0];
            bool hasSunglasses = face.Sunglasses?.Value ?? false;
            bool eyesOpen = (face.EyesOpen?.Value ?? true) && (face.EyesOpen?.Confidence > 70.0);

            if (hasSunglasses)
            {
                return new FaceDetectionResult
                {
                    IsValid = false,
                    FaceCount = 1,
                    HasSunglasses = true,
                    ErrorMessage = "Vui lòng tháo kính râm khi chụp ảnh selfie xác thực."
                };
            }

            return new FaceDetectionResult
            {
                IsValid = true,
                FaceCount = 1,
                Brightness = face.Quality?.Brightness ?? 100,
                Sharpness = face.Quality?.Sharpness ?? 100,
                HasSunglasses = false,
                EyesOpen = eyesOpen
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AWS Rekognition] Lỗi khi kiểm tra khuôn mặt selfie (DetectFaces)");
            return new FaceDetectionResult { IsValid = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<FaceMatchResult> CompareFacesAsync(byte[] sourceImageBytes, byte[] targetImageBytes, float similarityThreshold = 70.0f, CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new CompareFacesRequest
            {
                SourceImage = new Image { Bytes = new MemoryStream(sourceImageBytes) },
                TargetImage = new Image { Bytes = new MemoryStream(targetImageBytes) },
                SimilarityThreshold = similarityThreshold
            };

            var response = await _rekognitionClient.CompareFacesAsync(request, cancellationToken);

            if (response.FaceMatches.Count > 0)
            {
                var topMatch = response.FaceMatches.OrderByDescending(f => f.Similarity).First();
                _logger.LogInformation("[AWS Rekognition] So khớp khuôn mặt thành công: Tương đồng={Sim}%, Độ tin cậy={Conf}%",
                    topMatch.Similarity, topMatch.Face.Confidence);

                return new FaceMatchResult
                {
                    IsMatched = true,
                    SimilarityScore = topMatch.Similarity,
                    ConfidenceScore = topMatch.Face.Confidence
                };
            }

            _logger.LogWarning("[AWS Rekognition] Không tìm thấy khuôn mặt trùng khớp giữa 2 ảnh.");
            return new FaceMatchResult
            {
                IsMatched = false,
                SimilarityScore = 0.0f,
                ErrorMessage = "Khuôn mặt giữa ảnh chân dung thẻ CCCD và ảnh Selfie không trùng khớp."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AWS Rekognition] Lỗi khi so khớp khuôn mặt (CompareFaces)");
            return new FaceMatchResult { IsMatched = false, ErrorMessage = ex.Message };
        }
    }

    private static void ParseVietnameseIdCard(List<string> lines, OcrExtractionResult result)
    {
        // 1. Regex tìm số CCCD 12 chữ số hoặc CMND 9 chữ số
        var idRegex = new Regex(@"(?:Số|No|No\.|SỐ|ĐỊNH DANH|CAN CƯOC)?\s*:?\s*(\b\d{12}\b|\b\d{9}\b)", RegexOptions.IgnoreCase);
        // 2. Regex tìm ngày sinh (dd/MM/yyyy)
        var dobRegex = new Regex(@"(\b\d{2}[/\.-]\d{2}[/\.-]\d{4}\b)");

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(result.IdentityNumber))
            {
                var match = idRegex.Match(line);
                if (match.Success)
                {
                    result.IdentityNumber = match.Groups[1].Value;
                    continue;
                }
            }

            if (!result.DateOfBirth.HasValue)
            {
                var match = dobRegex.Match(line);
                if (match.Success && DateTime.TryParseExact(match.Groups[1].Value.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, System.Globalization.DateTimeStyles.None, out var dob))
                {
                    result.DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    continue;
                }
            }

            // Trích xuất họ và tên (dòng sau từ khóa Họ và tên hoặc Full name)
            if (string.IsNullOrEmpty(result.FullName) && (line.Contains("Họ và tên", StringComparison.OrdinalIgnoreCase) || line.Contains("Full name", StringComparison.OrdinalIgnoreCase) || line.Contains("Họ tên", StringComparison.OrdinalIgnoreCase)))
            {
                var namePart = line
                    .Replace("Họ và tên", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Full name", "", StringComparison.OrdinalIgnoreCase)
                    .Replace("Họ tên", "", StringComparison.OrdinalIgnoreCase)
                    .Replace(":", "").Trim();

                if (!string.IsNullOrEmpty(namePart) && namePart.Length >= 3)
                {
                    result.FullName = namePart.ToUpper();
                }
            }
        }
    }
}
