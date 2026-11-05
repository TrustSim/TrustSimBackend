namespace TrustSimBackend.Application.Common.Models;

/// <summary>
/// Kết quả kiểm định tính tương thích Font chữ đa thiết bị và bảng mã Unicode của tệp PDF.
/// </summary>
public record PdfFontAuditResultDto(
    bool IsCrossDeviceSafe,
    int TotalPages,
    int TotalFonts,
    int EmbeddedFontsCount,
    IReadOnlyList<string> UnembeddedFonts,
    IReadOnlyList<string> MissingUnicodeMapFonts,
    IReadOnlyList<string> LegacyNonUnicodeFonts,
    bool HasReplacementCharacters,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);
