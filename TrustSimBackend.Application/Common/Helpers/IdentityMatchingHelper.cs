using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TrustSimBackend.Application.Common.Helpers;

/// <summary>
/// Helper đối soát và chuẩn hóa thông tin danh tính (Số CCCD, Họ và tên tiếng Việt).
/// </summary>
public static class IdentityMatchingHelper
{
    /// <summary>
    /// So khớp số CMND/CCCD (chỉ giữ lại các ký tự số để so sánh).
    /// </summary>
    public static bool IsIdentityNumberMatch(string? ocrNumber, string? profileNumber)
    {
        if (string.IsNullOrWhiteSpace(profileNumber) || string.IsNullOrWhiteSpace(ocrNumber))
        {
            return true;
        }

        var cleanOcr = Regex.Replace(ocrNumber.Trim(), @"\D", "");
        var cleanProfile = Regex.Replace(profileNumber.Trim(), @"\D", "");
        return string.Equals(cleanOcr, cleanProfile, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// So khớp Họ và tên tiếng Việt:
    /// 1. Khớp nguyên bản có dấu chuẩn hóa (ví dụ: "NGUYỄN NHẬT QUÂN" == "Nguyễn Nhật Quân").
    /// 2. Khớp không dấu có khoảng trắng (ví dụ: "NGUYEN NHAT QUAN" == "Nguyen Nhat Quan").
    /// 3. Khớp không dấu dính liền / CamelCase (ví dụ: "NGUYEN NHAT QUAN" == "NguyenNhatQuan").
    /// </summary>
    public static bool IsFullNameMatch(string? ocrName, string? profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName) || string.IsNullOrWhiteSpace(ocrName))
        {
            return true;
        }

        var normOcr = NormalizeVietnameseName(ocrName);
        var normProfile = NormalizeVietnameseName(profileName);

        // 1. So khớp trực tiếp có dấu chuẩn hóa
        if (string.Equals(normOcr, normProfile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 2. So khớp không dấu
        var plainOcr = RemoveDiacritics(normOcr);
        var plainProfile = RemoveDiacritics(normProfile);
        if (string.Equals(plainOcr, plainProfile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // 3. So khớp không dấu và loại bỏ toàn bộ khoảng trắng / ký tự phân cách (hỗ trợ CamelCase như "NguyenNhatQuan")
        var compactOcr = Regex.Replace(plainOcr, @"[\s\p{P}]+", "");
        var compactProfile = Regex.Replace(plainProfile, @"[\s\p{P}]+", "");
        if (string.Equals(compactOcr, compactProfile, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Chuẩn hóa khoảng trắng và chuyển sang chữ hoa.
    /// </summary>
    public static string NormalizeVietnameseName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var clean = NormalizeHomoglyphs(name);
        return Regex.Replace(clean.Trim(), @"\s+", " ").ToUpperInvariant();
    }

    /// <summary>
    /// Loại bỏ dấu tiếng Việt (FormD Decomposition).
    /// </summary>
    public static string RemoveDiacritics(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var clean = NormalizeHomoglyphs(text);
        var normalizedString = clean.Normalize(NormalizationForm.FormD);
        var stringBuilder = new StringBuilder();
        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                stringBuilder.Append(c);
            }
        }

        return stringBuilder.ToString().Normalize(NormalizationForm.FormC).Replace('Đ', 'D').Replace('đ', 'd');
    }

    /// <summary>
    /// Chuyển đổi các ký tự Cyrillic tương đồng sang ký tự Latinh chuẩn.
    /// </summary>
    private static string NormalizeHomoglyphs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return text
            .Replace('\u041D', 'H')
            .Replace('\u043D', 'h')
            .Replace('\u041E', 'O')
            .Replace('\u043E', 'o')
            .Replace('\u0410', 'A')
            .Replace('\u0430', 'a')
            .Replace('\u0415', 'E')
            .Replace('\u0435', 'e')
            .Replace('\u0422', 'T')
            .Replace('\u0442', 't')
            .Replace('\u0421', 'C')
            .Replace('\u0441', 'c')
            .Replace('\u0420', 'P')
            .Replace('\u0440', 'p')
            .Replace('\u041C', 'M')
            .Replace('\u043C', 'm')
            .Replace('\u0412', 'B')
            .Replace('\u0432', 'b');
    }
}
