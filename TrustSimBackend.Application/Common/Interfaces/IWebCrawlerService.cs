namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ cào và làm sạch nội dung văn bản từ URL
/// </summary>
public interface IWebCrawlerService
{
    /// <summary>
    /// Tải trang web từ URL, loại bỏ các thẻ rác (script, style, nav, footer, ads) hoặc bóc tách file PDF/Docs và trả về văn bản sạch
    /// </summary>
    Task<string> FetchAndExtractCleanTextAsync(string url, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bóc tách văn bản sạch từ một Stream tệp tin (PDF, TXT, MD, HTML)
    /// </summary>
    Task<string> ExtractTextFromStreamAsync(Stream stream, string fileNameOrContentType, CancellationToken cancellationToken = default);
}
