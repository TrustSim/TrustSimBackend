using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;
using UglyToad.PdfPig;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class WebCrawlerService : IWebCrawlerService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebCrawlerService> _logger;

    public WebCrawlerService(HttpClient httpClient, ILogger<WebCrawlerService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        _httpClient.Timeout = TimeSpan.FromSeconds(45);
    }

    public async Task<string> FetchAndExtractCleanTextAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            var effectiveUrl = TransformUrlIfNeeded(url);
            _logger.LogInformation("Đang tải dữ liệu tài liệu từ URL: {Url} (Effective URL: {EffectiveUrl})", url, effectiveUrl);

            using var response = await _httpClient.GetAsync(effectiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

            // Kiểm tra magic bytes xem có phải là PDF hay không
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            var isPdf = contentType.Contains("pdf") || effectiveUrl.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || IsPdfStream(ms);
            ms.Position = 0;

            if (isPdf)
            {
                _logger.LogInformation("Phát hiện tài liệu định dạng PDF từ URL {Url}. Bắt đầu trích xuất bằng Native PDF Engine.", url);
                return ExtractTextFromPdfStream(ms);
            }

            if (contentType.Contains("text/plain") || effectiveUrl.Contains("format=txt") || effectiveUrl.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || effectiveUrl.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var rawTxt = await reader.ReadToEndAsync(cancellationToken);
                return CleanExtractedText(rawTxt);
            }

            // Mặc định phân tích cú pháp HTML
            using var htmlReader = new StreamReader(ms, Encoding.UTF8);
            var rawHtml = await htmlReader.ReadToEndAsync(cancellationToken);
            return ExtractTextFromHtml(rawHtml, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cào nội dung từ URL {Url}: {Message}", url, ex.Message);
            throw new InvalidOperationException($"Không thể tải nội dung từ URL '{url}': {ex.Message}", ex);
        }
    }

    public async Task<string> ExtractTextFromStreamAsync(Stream stream, string fileNameOrContentType, CancellationToken cancellationToken = default)
    {
        try
        {
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, cancellationToken);
            ms.Position = 0;

            var isPdf = fileNameOrContentType.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ||
                        fileNameOrContentType.Contains("pdf") ||
                        IsPdfStream(ms);
            ms.Position = 0;

            if (isPdf)
            {
                return ExtractTextFromPdfStream(ms);
            }

            if (fileNameOrContentType.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
                fileNameOrContentType.EndsWith(".htm", StringComparison.OrdinalIgnoreCase) ||
                fileNameOrContentType.Contains("text/html"))
            {
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var html = await reader.ReadToEndAsync(cancellationToken);
                return ExtractTextFromHtml(html, "UploadedFile");
            }

            // Plain text / Markdown / CSV
            using var textReader = new StreamReader(ms, Encoding.UTF8);
            var rawText = await textReader.ReadToEndAsync(cancellationToken);
            return CleanExtractedText(rawText);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi trích xuất text từ tệp '{FileName}': {Message}", fileNameOrContentType, ex.Message);
            throw new InvalidOperationException($"Không thể trích xuất văn bản từ tệp '{fileNameOrContentType}': {ex.Message}", ex);
        }
    }

    private static string TransformUrlIfNeeded(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl;
        var trimmed = rawUrl.Trim();

        // 1. Google Docs URL
        // https://docs.google.com/document/d/{DOC_ID}/edit... -> https://docs.google.com/document/d/{DOC_ID}/export?format=txt
        var docsMatch = Regex.Match(trimmed, @"docs\.google\.com/document/d/([a-zA-Z0-9_-]+)");
        if (docsMatch.Success)
        {
            var docId = docsMatch.Groups[1].Value;
            return $"https://docs.google.com/document/d/{docId}/export?format=txt";
        }

        // 2. Google Drive File URL
        // https://drive.google.com/file/d/{FILE_ID}/view... -> https://drive.google.com/uc?export=download&id={FILE_ID}
        var driveFileMatch = Regex.Match(trimmed, @"drive\.google\.com/file/d/([a-zA-Z0-9_-]+)");
        if (driveFileMatch.Success)
        {
            var fileId = driveFileMatch.Groups[1].Value;
            return $"https://drive.google.com/uc?export=download&id={fileId}";
        }

        // 3. Google Drive Open/UC URL
        var driveOpenMatch = Regex.Match(trimmed, @"drive\.google\.com/open\?id=([a-zA-Z0-9_-]+)");
        if (driveOpenMatch.Success)
        {
            var fileId = driveOpenMatch.Groups[1].Value;
            return $"https://drive.google.com/uc?export=download&id={fileId}";
        }

        return trimmed;
    }

    private static bool IsPdfStream(Stream stream)
    {
        if (stream.Length < 4) return false;
        var buffer = new byte[4];
        var read = stream.Read(buffer, 0, 4);
        stream.Position = 0;
        return read == 4 && buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46; // %PDF
    }

    private string ExtractTextFromPdfStream(Stream stream)
    {
        try
        {
            using var pdf = PdfDocument.Open(stream);
            var sb = new StringBuilder();

            int pageNumber = 1;
            foreach (var page in pdf.GetPages())
            {
                var pageText = page.Text;
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    sb.AppendLine($"--- [Trang {pageNumber}] ---");
                    sb.AppendLine(pageText.Trim());
                    sb.AppendLine();
                }
                pageNumber++;
            }

            var clean = CleanExtractedText(sb.ToString());
            _logger.LogInformation("Trích xuất thành công {Pages} trang PDF, tổng cộng {Length} ký tự.", pdf.NumberOfPages, clean.Length);
            return clean;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi giải mã PDF bằng PdfPig: {Message}", ex.Message);
            throw new InvalidOperationException($"Lỗi giải mã tài liệu PDF: {ex.Message}", ex);
        }
    }

    private string ExtractTextFromHtml(string rawHtml, string url)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(rawHtml);

        // Loại bỏ các node không chứa nội dung chính
        var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside|//form|//noscript|//svg|//iframe|//head");
        if (nodesToRemove != null)
        {
            foreach (var node in nodesToRemove)
            {
                node.Remove();
            }
        }

        // Ưu tiên trích xuất từ thẻ article hoặc main nếu có
        var mainContentNode = doc.DocumentNode.SelectSingleNode("//article")
                            ?? doc.DocumentNode.SelectSingleNode("//main")
                            ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'content') or contains(@class, 'post') or contains(@class, 'article') or contains(@id, 'content')]")
                            ?? doc.DocumentNode.SelectSingleNode("//body")
                            ?? doc.DocumentNode;

        var sb = new StringBuilder();
        ExtractTextRecursive(mainContentNode, sb);

        var cleanText = CleanExtractedText(sb.ToString());
        _logger.LogInformation("Tải và bóc tách thành công {Length} ký tự HTML từ URL {Url}", cleanText.Length, url);
        return cleanText;
    }

    private static string CleanExtractedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var extracted = Regex.Replace(text, @"[ \t]+", " ");
        extracted = Regex.Replace(extracted, @"(\r?\n\s*){3,}", "\n\n");
        return extracted.Trim();
    }

    private static void ExtractTextRecursive(HtmlNode node, StringBuilder sb)
    {
        if (node.NodeType == HtmlNodeType.Text)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText).Trim();
            if (!string.IsNullOrEmpty(text))
            {
                sb.Append(text).Append(' ');
            }
            return;
        }

        var isBlock = node.Name is "p" or "div" or "h1" or "h2" or "h3" or "h4" or "h5" or "h6" or "li" or "tr" or "br" or "blockquote";

        if (isBlock && sb.Length > 0 && !sb.ToString().EndsWith('\n'))
        {
            sb.AppendLine();
        }

        foreach (var child in node.ChildNodes)
        {
            ExtractTextRecursive(child, sb);
        }

        if (isBlock && sb.Length > 0 && !sb.ToString().EndsWith('\n'))
        {
            sb.AppendLine();
        }
    }
}
