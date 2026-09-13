using System.Text.Json;
using TrustSimBackend.Application.Common.Interfaces;
using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Infrastructure.Services.Ai;

public class TextChunkerService : ITextChunkerService
{
    public IReadOnlyList<TextChunkItem> ChunkText(string rawText, int chunkSize = 600, int overlapSize = 100, string? documentName = null)
    {
        var results = new List<TextChunkItem>();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            return results;
        }

        // Tách thành các đoạn văn (paragraphs)
        var paragraphs = rawText
            .Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (paragraphs.Count == 0)
        {
            paragraphs.Add(rawText.Trim());
        }

        var currentWords = new List<string>();
        int chunkIndex = 0;

        foreach (var paragraph in paragraphs)
        {
            var pWords = paragraph.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            if (currentWords.Count + pWords.Length > chunkSize && currentWords.Count > 0)
            {
                // Tạo chunk hiện tại
                var chunkContent = string.Join(" ", currentWords);
                int tokenEstimate = (int)Math.Ceiling(currentWords.Count * 1.3);

                var meta = new
                {
                    documentName = documentName ?? "Document",
                    chunkIndex = chunkIndex,
                    wordCount = currentWords.Count,
                    createdAt = DateTime.UtcNow
                };

                results.Add(new TextChunkItem
                {
                    ChunkIndex = chunkIndex++,
                    Content = chunkContent,
                    TokenCount = tokenEstimate,
                    MetadataJson = JsonSerializer.Serialize(meta)
                });

                // Giữ lại phần overlap cho chunk tiếp theo
                var overlapCount = Math.Min(overlapSize, currentWords.Count);
                currentWords = currentWords.Skip(currentWords.Count - overlapCount).ToList();
            }

            currentWords.AddRange(pWords);
        }

        // Lưu chunk cuối cùng còn sót lại
        if (currentWords.Count > 0)
        {
            var chunkContent = string.Join(" ", currentWords);
            int tokenEstimate = (int)Math.Ceiling(currentWords.Count * 1.3);

            var meta = new
            {
                documentName = documentName ?? "Document",
                chunkIndex = chunkIndex,
                wordCount = currentWords.Count,
                createdAt = DateTime.UtcNow
            };

            results.Add(new TextChunkItem
            {
                ChunkIndex = chunkIndex,
                Content = chunkContent,
                TokenCount = tokenEstimate,
                MetadataJson = JsonSerializer.Serialize(meta)
            });
        }

        return results;
    }
}
