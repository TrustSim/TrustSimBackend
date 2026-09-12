using System.Globalization;
using System.Text;
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
                OverallConfidence = response.TextDetections.Count > 0 ? response.TextDetections.Average(x => x.Confidence) : 0
            };

            ParseVietnameseIdCard(lines, result);

            var jsonOptions = new JsonSerializerOptions
            {
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            };

            var ocrPayload = new
            {
                extractedInfo = new
                {
                    identityNumber = result.IdentityNumber,
                    fullName = result.FullName,
                    dateOfBirth = result.DateOfBirth?.ToString("dd/MM/yyyy"),
                    gender = result.Gender,
                    nationality = result.Nationality,
                    placeOfOrigin = result.PlaceOfOrigin,
                    placeOfResidence = result.PlaceOfResidence ?? result.Address,
                    expiryDate = result.ExpiryDate?.ToString("dd/MM/yyyy")
                },
                confidenceScore = Math.Round(result.OverallConfidence, 2),
                totalLinesDetected = lines.Count,
                detectedLines = lines
            };

            result.RawJson = JsonSerializer.Serialize(ocrPayload, jsonOptions);

            _logger.LogInformation("[AWS Rekognition] Trích xuất OCR thành công: CCCD={Id}, Tên={Name}, NS={Dob}, Giới tính={Sex}, Quốc tịch={Nat}, Quê quán={Origin}, Thường trú={Res}",
                result.IdentityNumber, result.FullName, result.DateOfBirth, result.Gender, result.Nationality, result.PlaceOfOrigin, result.PlaceOfResidence);

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
        // 2. Regex tìm ngày tháng năm (dd/MM/yyyy)
        var dateRegex = new Regex(@"(\b\d{2}[/\.-]\d{2}[/\.-]\d{4}\b)");
        // 3. Regex tìm giới tính
        var genderRegex = new Regex(@"(?:Gi[oó]i\s*t[ií]nh|Sex)\s*:?\s*(Nam|N[uữ]|Male|Female)", RegexOptions.IgnoreCase);
        // 4. Regex tìm quốc tịch
        var nationalityRegex = new Regex(@"(?:Qu[oố]c\s*t[iị]ch|Nationality)\s*:?\s*(Vi[eêệ]t\s*Nam|Viet\s*Nam)", RegexOptions.IgnoreCase);

        var residenceLines = new List<string>();
        int residenceStartIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var rawLine = lines[i]?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var normalizedLine = NormalizeHomoglyphs(CleanOcrNoise(rawLine));

            // Bóc tách Số CCCD
            if (string.IsNullOrEmpty(result.IdentityNumber))
            {
                var match = idRegex.Match(normalizedLine);
                if (match.Success)
                {
                    result.IdentityNumber = match.Groups[1].Value;
                }
            }

            // Bóc tách Ngày sinh
            if (!result.DateOfBirth.HasValue)
            {
                var plainLine = RemoveDiacritics(normalizedLine).ToUpperInvariant();
                if (plainLine.Contains("NGAY SINH") || plainLine.Contains("DATE OF BIRTH") || plainLine.Contains("DOB") || plainLine.Contains("NAM SINH"))
                {
                    var match = dateRegex.Match(normalizedLine);
                    if (match.Success && DateTime.TryParseExact(match.Groups[1].Value.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, DateTimeStyles.None, out var dob))
                    {
                        result.DateOfBirth = DateTime.SpecifyKind(dob, DateTimeKind.Utc);
                    }
                    else if (i + 1 < lines.Count)
                    {
                        var nextMatch = dateRegex.Match(lines[i + 1]);
                        if (nextMatch.Success && DateTime.TryParseExact(nextMatch.Groups[1].Value.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, DateTimeStyles.None, out var nextDob))
                        {
                            result.DateOfBirth = DateTime.SpecifyKind(nextDob, DateTimeKind.Utc);
                        }
                    }
                }
            }

            // Bóc tách Họ và tên từ dòng nhãn
            if (string.IsNullOrEmpty(result.FullName) && IsNameLabel(normalizedLine))
            {
                var cleanedSameLine = Regex.Replace(normalizedLine, @"(?:H[oọ\u041E\u043E](?:\s*(?:và|chữ\s*đệm\s*và)?\s*tên|\s*tên)?|Full\s*name|khai\s*sinh|[:/,\-])", " ", RegexOptions.IgnoreCase);
                cleanedSameLine = Regex.Replace(cleanedSameLine, @"\s+", " ").Trim();

                if (IsValidPersonName(cleanedSameLine))
                {
                    result.FullName = FormatProperFullName(cleanedSameLine);
                }
                else
                {
                    for (int k = i + 1; k <= Math.Min(i + 2, lines.Count - 1); k++)
                    {
                        var candidate = NormalizeHomoglyphs(CleanOcrNoise(lines[k])).Trim();
                        if (IsValidPersonName(candidate))
                        {
                            result.FullName = FormatProperFullName(candidate);
                            break;
                        }
                    }
                }
            }

            // Bóc tách Giới tính
            if (string.IsNullOrEmpty(result.Gender))
            {
                var match = genderRegex.Match(normalizedLine);
                if (match.Success)
                {
                    var gVal = match.Groups[1].Value.Trim();
                    result.Gender = (gVal.Equals("Nam", StringComparison.OrdinalIgnoreCase) || gVal.Equals("Male", StringComparison.OrdinalIgnoreCase)) ? "Nam" : "Nữ";
                }
            }

            // Bóc tách Quốc tịch
            if (string.IsNullOrEmpty(result.Nationality))
            {
                var match = nationalityRegex.Match(normalizedLine);
                if (match.Success)
                {
                    result.Nationality = "Việt Nam";
                }
                else
                {
                    var plainLine = RemoveDiacritics(normalizedLine).ToUpperInvariant();
                    if (plainLine.Contains("QUOC TICH") || plainLine.Contains("NATIONALITY"))
                    {
                        if (i + 1 < lines.Count)
                        {
                            var nextPlain = RemoveDiacritics(lines[i + 1]).ToUpperInvariant();
                            if (nextPlain.Contains("VIET NAM"))
                            {
                                result.Nationality = "Việt Nam";
                            }
                        }
                    }
                }
            }

            // Bóc tách Quê quán
            if (string.IsNullOrEmpty(result.PlaceOfOrigin))
            {
                var plainLine = RemoveDiacritics(normalizedLine).ToUpperInvariant();
                if (plainLine.Contains("QUE QUAN") || plainLine.Contains("PLACE OF ORIGIN") || plainLine.Contains("NOI SINH"))
                {
                    var cleaned = Regex.Replace(normalizedLine, @"(?:Qu[eê]\s*qu[aá]n|Place\s*of\s*origin|N[oơ]i\s*sinh|[:/,\-])", " ", RegexOptions.IgnoreCase).Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 3)
                    {
                        result.PlaceOfOrigin = CleanOcrNoise(cleaned);
                    }
                    else if (i + 1 < lines.Count && !IsAnyCardLabel(lines[i + 1]))
                    {
                        result.PlaceOfOrigin = CleanOcrNoise(lines[i + 1].Trim());
                    }
                }
            }

            // Bóc tách Nơi thường trú (Place of Residence)
            var plainLineResidence = RemoveDiacritics(normalizedLine).ToUpperInvariant();
            if (plainLineResidence.Contains("NOI THUONG TRU") || plainLineResidence.Contains("PLACE OF RESIDENCE") || plainLineResidence.Contains("NOI CU TRU"))
            {
                residenceStartIndex = i;
                var cleaned = Regex.Replace(normalizedLine, @"(?:N[oơ]i\s*th[uư][oờ]ng\s*tr[uú]|Place\s*of\s*residence|N[oơ]i\s*c[uư]\s*tr[uú]|[:/\-])", " ", RegexOptions.IgnoreCase).Trim();
                if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 2)
                {
                    residenceLines.Add(CleanOcrNoise(cleaned));
                }
            }
            else if (residenceStartIndex >= 0 && i > residenceStartIndex && i <= residenceStartIndex + 4)
            {
                if (!IsAnyCardLabel(normalizedLine))
                {
                    var cleaned = CleanOcrNoise(normalizedLine).Trim();
                    if (!string.IsNullOrWhiteSpace(cleaned) && !dateRegex.IsMatch(cleaned))
                    {
                        residenceLines.Add(cleaned);
                    }
                }
            }

            // Bóc tách Ngày hết hạn (Date of Expiry)
            if (!result.ExpiryDate.HasValue)
            {
                var plainLine = RemoveDiacritics(normalizedLine).ToUpperInvariant();
                if (plainLine.Contains("CO GIA TRI DEN") || plainLine.Contains("DATE OF EXPIRY") || plainLine.Contains("HET HAN"))
                {
                    var match = dateRegex.Match(normalizedLine);
                    if (match.Success && DateTime.TryParseExact(match.Groups[1].Value.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, DateTimeStyles.None, out var exp))
                    {
                        result.ExpiryDate = DateTime.SpecifyKind(exp, DateTimeKind.Utc);
                    }
                    else if (i + 1 < lines.Count)
                    {
                        var nextMatch = dateRegex.Match(lines[i + 1]);
                        if (nextMatch.Success && DateTime.TryParseExact(nextMatch.Groups[1].Value.Replace("-", "/").Replace(".", "/"), "dd/MM/yyyy", null, DateTimeStyles.None, out var nextExp))
                        {
                            result.ExpiryDate = DateTime.SpecifyKind(nextExp, DateTimeKind.Utc);
                        }
                    }
                }
            }
        }

        // Kết hợp các dòng Nơi thường trú
        if (residenceLines.Count > 0)
        {
            var joined = string.Join(", ", residenceLines.Distinct()).Replace(", ,", ",").Trim(',', ' ');
            result.PlaceOfResidence = joined;
            result.Address = joined;
        }
        else if (!string.IsNullOrEmpty(result.PlaceOfOrigin))
        {
            result.Address = result.PlaceOfOrigin;
        }

        // Fallback: Tìm số CCCD nếu chưa có
        if (string.IsNullOrEmpty(result.IdentityNumber))
        {
            foreach (var line in lines)
            {
                var match = idRegex.Match(NormalizeHomoglyphs(line));
                if (match.Success)
                {
                    result.IdentityNumber = match.Groups[1].Value;
                    break;
                }
            }
        }

        // Fallback: Tìm Họ tên nếu chưa có
        if (string.IsNullOrEmpty(result.FullName))
        {
            for (int i = 0; i < lines.Count; i++)
            {
                var candidate = NormalizeHomoglyphs(CleanOcrNoise(lines[i])).Trim();
                if (IsValidPersonName(candidate))
                {
                    var plain = RemoveDiacritics(candidate);
                    if (plain == plain.ToUpperInvariant())
                    {
                        result.FullName = FormatProperFullName(candidate);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Kiểm tra xem dòng văn bản có phải là nhãn tiêu đề Họ và tên hay không.
    /// </summary>
    private static bool IsNameLabel(string line)
    {
        var plain = RemoveDiacritics(NormalizeHomoglyphs(line)).ToUpperInvariant();
        return Regex.IsMatch(plain, @"\b(HO\s*(?:VA\s*)?(?:(?:VA\s*)?CHU\s*DEM\s*(?:VA\s*)?)?TEN|FULL\s*NAME|KHAI\s*SINH|HO\s*TEN)\b");
    }

    /// <summary>
    /// Kiểm tra xem dòng văn bản có phải là nhãn hệ thống hoặc tiêu đề trên thẻ CCCD/CMND hay không.
    /// </summary>
    private static bool IsAnyCardLabel(string text)
    {
        var plain = RemoveDiacritics(NormalizeHomoglyphs(text)).ToUpperInvariant();

        if (Regex.IsMatch(plain, @"\b(HO\s*(?:VA\s*)?(?:CHU\s*DEM\s*(?:VA\s*)?)?TEN|FULL\s*NAME|KHAI\s*SINH|HO\s*TEN)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(NGAY\s*SINH|DATE\s*OF\s*BIRTH|DOB|NAM\s*SINH)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(GIOI\s*TINH|SEX|GENDER|QUOC\s*TICH|NATIONALITY)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(QUE\s*QUAN|PLACE\s*OF\s*ORIGIN|NOI\s*SINH|PLACE\s*OF\s*BIRTH)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(NOI\s*THUONG\s*TRU|NOI\s*CU\s*TRU|PLACE\s*OF\s*RESIDENCE|RESIDENCE)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(CO\s*GIA\s*TRI\s*DEN|DATE\s*OF\s*EXPIRY|EXPIRY|HET\s*HAN)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(CONG\s*HOA\s*XA\s*HOI|VIET\s*NAM|DOC\s*LAP|TU\s*DO|HANH\s*PHUC)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(CAN\s*CUOC\s*CONG\s*DAN|CAN\s*CUOC|CITIZEN\s*IDENTITY|IDENTITY\s*CARD|CHUNG\s*MINH\s*NHAN\s*DAN)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(SO|NO|SO\s*DINH\s*DANH|IDENTITY\s*NO)\b")) return true;
        if (Regex.IsMatch(plain, @"\b(DAC\s*DIEM\s*NHAN\s*DANG|NGAY\s*CAP|CUC\s*TRUONG)\b")) return true;

        return false;
    }

    /// <summary>
    /// Kiểm tra chuỗi có thỏa mãn cấu trúc Họ và tên người Việt Nam hay không.
    /// </summary>
    private static bool IsValidPersonName(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = Regex.Replace(text.Trim(), @"\s+", " ");
        if (clean.Length < 3 || clean.Length > 50) return false;

        // Không được chứa chữ số
        if (clean.Any(char.IsDigit)) return false;

        // Không được chứa các ký tự đặc biệt
        if (clean.IndexOfAny(new[] { '/', ':', '@', '#', '$', '%', '^', '&', '*', '(', ')', '<', '>', '{', '}', '[', ']', '=', '+', '_' }) >= 0)
        {
            return false;
        }

        // Không được là nhãn thẻ CCCD
        if (IsAnyCardLabel(clean)) return false;

        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length >= 2 && words.Length <= 6;
    }

    /// <summary>
    /// Chuẩn hóa và làm sạch nhiễu OCR (các ký tự nháy đơn, macron, gạch nối bị nhận diện sai).
    /// </summary>
    private static string CleanOcrNoise(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace("''", "")
            .Replace("'", "")
            .Replace("`", "")
            .Replace("\"", "")
            .Replace("\u0112", "Ê")
            .Replace("\u0113", "ê")
            .Replace("\u016B", "u")
            .Replace("\u016A", "U")
            .Replace("  ", " ")
            .Trim();
    }

    /// <summary>
    /// Định dạng họ và tên in hoa chuẩn tiếng Việt.
    /// </summary>
    private static string FormatProperFullName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        var clean = Regex.Replace(CleanOcrNoise(NormalizeHomoglyphs(name)), @"\s+", " ").Trim();
        return clean.ToUpperInvariant();
    }

    /// <summary>
    /// Chuyển đổi các ký tự Cyrillic tương đồng (Homoglyphs) sang ký tự Latinh chuẩn.
    /// </summary>
    private static string NormalizeHomoglyphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace('\u041D', 'H') // Cyrillic capital En -> H
            .Replace('\u043D', 'h') // Cyrillic small En -> h
            .Replace('\u041E', 'O') // Cyrillic capital O -> O
            .Replace('\u043E', 'o') // Cyrillic small O -> o
            .Replace('\u0410', 'A') // Cyrillic capital A -> A
            .Replace('\u0430', 'a') // Cyrillic small A -> a
            .Replace('\u0415', 'E') // Cyrillic capital Ie -> E
            .Replace('\u0435', 'e') // Cyrillic small Ie -> e
            .Replace('\u0422', 'T') // Cyrillic capital Te -> T
            .Replace('\u0442', 't') // Cyrillic small Te -> t
            .Replace('\u0421', 'C') // Cyrillic capital Es -> C
            .Replace('\u0441', 'c') // Cyrillic small Es -> c
            .Replace('\u0420', 'P') // Cyrillic capital Er -> P
            .Replace('\u0440', 'p') // Cyrillic small Er -> p
            .Replace('\u041C', 'M') // Cyrillic capital Em -> M
            .Replace('\u043C', 'm') // Cyrillic small Em -> m
            .Replace('\u0412', 'B') // Cyrillic capital Ve -> B
            .Replace('\u0432', 'b'); // Cyrillic small Ve -> b
    }

    /// <summary>
    /// Loại bỏ dấu tiếng Việt để so sánh chuỗi không dấu.
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalizedString = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(c);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(c);
            }
        }
        return sb.ToString().Normalize(NormalizationForm.FormC).Replace('Đ', 'D').Replace('đ', 'd');
    }
}
