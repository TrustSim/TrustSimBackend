using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Exceptions;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;
using ITextPdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace TrustSimBackend.Infrastructure.Services;

/// <summary>
/// Triển khai dịch vụ kiểm định Font chữ PDF đa thiết bị sử dụng kết hợp iText7 (kiểm tra Dictionary /Font, /FontDescriptor, /FontFile*, /ToUnicode)
/// và PdfPig (kiểm tra giải mã văn bản Unicode thực tế).
/// </summary>
public class PdfFontValidatorService : IPdfFontValidatorService
{
    private readonly ILogger<PdfFontValidatorService> _logger;

    // Nhận diện các bộ font tiếng Việt 8-bit/16-bit cũ (TCVN3, VNI, BK HCM, ABC) không chuẩn Unicode
    private static readonly Regex LegacyFontNameRegex = new(
        @"(^|\+)(\.?Vn|VNI-|VK\s?Sans|ABC|BKHCM)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PdfFontValidatorService(ILogger<PdfFontValidatorService> logger)
    {
        _logger = logger;
    }

    public Task<PdfFontAuditResultDto> AuditPdfStreamAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        if (pdfStream == null || pdfStream.Length < 4)
        {
            throw new InvalidPdfFontException("Tệp PDF rỗng hoặc không hợp lệ.");
        }

        pdfStream.Position = 0;
        var header = new byte[4];
        var read = pdfStream.Read(header, 0, 4);
        pdfStream.Position = 0;

        if (read < 4 || header[0] != 0x25 || header[1] != 0x50 || header[2] != 0x44 || header[3] != 0x46)
        {
            throw new InvalidPdfFontException("Tệp tải lên không đúng định dạng PDF chuẩn (sai chữ ký Magic Bytes %PDF).");
        }

        var unembeddedFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var missingUnicodeFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var legacyFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();
        var errors = new List<string>();

        int totalPages;
        int embeddedCount = 0;

        // 1. Kiểm tra Dictionary Font cấp thấp bằng iText7
        try
        {
            using var leaveOpenStream = new MemoryStream();
            pdfStream.CopyTo(leaveOpenStream);
            leaveOpenStream.Position = 0;
            pdfStream.Position = 0;

            using var reader = new PdfReader(leaveOpenStream);
            using var pdfDoc = new ITextPdfDocument(reader);
            totalPages = pdfDoc.GetNumberOfPages();

            for (int pageNum = 1; pageNum <= totalPages; pageNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var page = pdfDoc.GetPage(pageNum);
                var resources = page.GetResources();
                if (resources != null)
                {
                    InspectResourcesFonts(
                        resources.GetPdfObject(),
                        allFonts,
                        unembeddedFonts,
                        missingUnicodeFonts,
                        legacyFonts,
                        ref embeddedCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfFontAudit] Lỗi khi đọc cấu trúc PDF bằng iText7: {Message}", ex.Message);
            throw new InvalidPdfFontException($"Không thể đọc cấu trúc tệp PDF (tệp bị hỏng hoặc được đặt mật khẩu bảo vệ): {ex.Message}", ex);
        }

        // 2. Kiểm tra trích xuất Unicode thực tế bằng PdfPig
        bool hasReplacementChars = false;
        try
        {
            pdfStream.Position = 0;
            using var pigStream = new MemoryStream();
            pdfStream.CopyTo(pigStream);
            pigStream.Position = 0;
            pdfStream.Position = 0;

            using var pigDoc = PdfPigDocument.Open(pigStream);
            foreach (var page in pigDoc.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var text = page.Text;
                if (!string.IsNullOrEmpty(text) && text.Contains('\uFFFD'))
                {
                    hasReplacementChars = true;
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[PdfFontAudit] Cảnh báo khi kiểm tra giải mã văn bản bằng PdfPig: {Message}", ex.Message);
            warnings.Add($"Không thể trích xuất đầy đủ lớp văn bản bằng PdfPig: {ex.Message}");
        }

        if (legacyFonts.Count > 0)
        {
            errors.Add($"Phát hiện font bảng mã cũ không chuẩn Unicode ({string.Join(", ", legacyFonts)}). Văn bản sẽ bị lỗi font trên điện thoại và trình duyệt Web.");
        }

        if (unembeddedFonts.Count > 0)
        {
            errors.Add($"Phát hiện {unembeddedFonts.Count} font chưa được nhúng (Unembedded) vào tệp PDF ({string.Join(", ", unembeddedFonts)}). Khi mở trên thiết bị không cài sẵn font này sẽ bị lỗi ô vuông hoặc lệch dấu.");
        }

        if (missingUnicodeFonts.Count > 0)
        {
            warnings.Add($"Phát hiện {missingUnicodeFonts.Count} font thiếu bảng ánh xạ /ToUnicode CMap ({string.Join(", ", missingUnicodeFonts)}), có thể gây sai lệch khi trích xuất văn bản AI RAG hoặc tìm kiếm.");
        }

        if (hasReplacementChars)
        {
            errors.Add("Phát hiện ký tự lỗi giải mã Unicode (U+FFFD) trong nội dung văn bản PDF.");
        }

        var isSafe = errors.Count == 0;

        return Task.FromResult(new PdfFontAuditResultDto(
            IsCrossDeviceSafe: isSafe,
            TotalPages: totalPages,
            TotalFonts: allFonts.Count,
            EmbeddedFontsCount: allFonts.Count - unembeddedFonts.Count,
            UnembeddedFonts: unembeddedFonts.ToList(),
            MissingUnicodeMapFonts: missingUnicodeFonts.ToList(),
            LegacyNonUnicodeFonts: legacyFonts.ToList(),
            HasReplacementCharacters: hasReplacementChars,
            Warnings: warnings,
            Errors: errors));
    }

    public async Task EnsureCrossDeviceCompatibleAsync(Stream pdfStream, string fileName, CancellationToken cancellationToken = default)
    {
        var audit = await AuditPdfStreamAsync(pdfStream, cancellationToken);
        if (!audit.IsCrossDeviceSafe)
        {
            var detail = string.Join(" ", audit.Errors);
            _logger.LogWarning("[PdfFontAudit] Từ chối tệp PDF '{FileName}' do không đạt chuẩn tương thích đa thiết bị: {Errors}", fileName, detail);
            throw new InvalidPdfFontException(
                $"Tệp PDF '{fileName}' không đạt chuẩn hiển thị đa thiết bị: {detail} " +
                "Vui lòng sử dụng font Unicode chuẩn (Times New Roman, Arial, Roboto, DejaVu Sans...) và bật tùy chọn 'Embed fonts in the file' (hoặc xuất chuẩn PDF/A) khi lưu PDF.");
        }
    }

    private static void InspectResourcesFonts(
        PdfDictionary? resourcesDict,
        HashSet<string> allFonts,
        HashSet<string> unembeddedFonts,
        HashSet<string> missingUnicodeFonts,
        HashSet<string> legacyFonts,
        ref int embeddedCount)
    {
        if (resourcesDict == null) return;

        var fontDict = resourcesDict.GetAsDictionary(PdfName.Font);
        if (fontDict != null)
        {
            foreach (var key in fontDict.KeySet())
            {
                var fontObj = fontDict.GetAsDictionary(key);
                if (fontObj == null) continue;

                var baseFontName = fontObj.GetAsName(PdfName.BaseFont)?.GetValue() ?? "UnknownFont";
                allFonts.Add(baseFontName);

                // 1. Kiểm tra tên font bảng mã cũ (TCVN3 / VNI)
                if (LegacyFontNameRegex.IsMatch(baseFontName))
                {
                    legacyFonts.Add(baseFontName);
                }

                // 2. Kiểm tra nhúng font (FontDescriptor -> FontFile / FontFile2 / FontFile3)
                bool isEmbedded = IsFontEmbedded(fontObj);
                if (!isEmbedded)
                {
                    unembeddedFonts.Add(baseFontName);
                }
                else
                {
                    embeddedCount++;
                }

                // 3. Kiểm tra /ToUnicode CMap đối với Composite Font (Type0)
                var subtype = fontObj.GetAsName(PdfName.Subtype);
                var hasToUnicode = fontObj.ContainsKey(PdfName.ToUnicode);
                if (PdfName.Type0.Equals(subtype) && !hasToUnicode)
                {
                    missingUnicodeFonts.Add(baseFontName);
                }
            }
        }

        // Đệ quy kiểm tra Form XObjects nếu trang PDF có nhúng biểu mẫu con
        var xObjectDict = resourcesDict.GetAsDictionary(PdfName.XObject);
        if (xObjectDict != null)
        {
            foreach (var key in xObjectDict.KeySet())
            {
                var xObjStream = xObjectDict.GetAsStream(key);
                if (xObjStream != null && PdfName.Form.Equals(xObjStream.GetAsName(PdfName.Subtype)))
                {
                    InspectResourcesFonts(
                        xObjStream.GetAsDictionary(PdfName.Resources),
                        allFonts,
                        unembeddedFonts,
                        missingUnicodeFonts,
                        legacyFonts,
                        ref embeddedCount);
                }
            }
        }
    }

    private static bool IsFontEmbedded(PdfDictionary fontObj)
    {
        var subtype = fontObj.GetAsName(PdfName.Subtype);

        // Nếu là Type0 (Composite Font / Identity-H), FontDescriptor nằm trong DescendantFonts[0]
        if (PdfName.Type0.Equals(subtype))
        {
            var descendants = fontObj.GetAsArray(PdfName.DescendantFonts);
            if (descendants != null && descendants.Size() > 0)
            {
                var cidFont = descendants.GetAsDictionary(0);
                var cidDescriptor = cidFont?.GetAsDictionary(PdfName.FontDescriptor);
                return HasFontFileStream(cidDescriptor);
            }
            return false;
        }

        // Nếu là Type3 (glyph vẽ bằng vector graphics trong CharProcs) thì luôn hiển thị được
        if (PdfName.Type3.Equals(subtype))
        {
            return true;
        }

        // TrueType, Type1, MMType1
        var descriptor = fontObj.GetAsDictionary(PdfName.FontDescriptor);
        return HasFontFileStream(descriptor);
    }

    private static bool HasFontFileStream(PdfDictionary? fontDescriptor)
    {
        if (fontDescriptor == null) return false;
        return fontDescriptor.ContainsKey(PdfName.FontFile)
            || fontDescriptor.ContainsKey(PdfName.FontFile2)
            || fontDescriptor.ContainsKey(PdfName.FontFile3);
    }
}
