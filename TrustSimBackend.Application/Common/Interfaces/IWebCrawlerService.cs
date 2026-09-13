namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ cào và làm sạch nội dung văn bản từ URL
/// </summary>
public interface IWebCrawlerService
{
    /// <summary>
    /// Tải trang web từ URL, loại bỏ các thẻ rác (script, style, nav, footer, ads) và trả về văn bản sạch
    /// </summary>
    Task<string> FetchAndExtractCleanTextAsync(string url, CancellationToken cancellationToken = default);
}
