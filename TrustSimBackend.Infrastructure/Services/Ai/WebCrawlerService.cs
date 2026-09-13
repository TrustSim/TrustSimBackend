using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using TrustSimBackend.Application.Common.Interfaces;

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
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TrustSimAiCrawler", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<string> FetchAndExtractCleanTextAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Đang tải dữ liệu tài liệu từ URL: {Url}", url);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rawHtml = await response.Content.ReadAsStringAsync(cancellationToken);

            var doc = new HtmlDocument();
            doc.LoadHtml(rawHtml);

            // Loại bỏ các node không chứa nội dung chính
            var nodesToRemove = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//aside|//form|//noscript|//svg|//iframe");
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

            var extracted = sb.ToString();

            // Làm sạch khoảng trắng thừa và dòng trống liên tiếp
            extracted = Regex.Replace(extracted, @"[ \t]+", " ");
            extracted = Regex.Replace(extracted, @"(\r?\n\s*){3,}", "\n\n");

            var cleanText = extracted.Trim();
            _logger.LogInformation("Tải và bóc tách thành công {Length} ký tự từ URL {Url}", cleanText.Length, url);
            return cleanText;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cào nội dung từ URL {Url}: {Message}", url, ex.Message);
            throw new InvalidOperationException($"Không thể tải nội dung từ URL '{url}': {ex.Message}", ex);
        }
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
