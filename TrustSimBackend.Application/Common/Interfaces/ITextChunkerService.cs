using TrustSimBackend.Application.Common.Models;

namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Dịch vụ chia nhỏ văn bản (Chunking) thông minh theo đoạn/câu có độ chồng lấn (overlap)
/// </summary>
public interface ITextChunkerService
{
    /// <summary>
    /// Bẻ nhỏ văn bản thô thành danh sách các đoạn trích (Chunks)
    /// </summary>
    /// <param name="rawText">Nội dung văn bản sạch</param>
    /// <param name="chunkSize">Kích thước mỗi đoạn theo số từ/token (mặc định: 600 từ ~ 800 tokens)</param>
    /// <param name="overlapSize">Kích thước phần chồng lấn (mặc định: 100 từ ~ 150 tokens)</param>
    /// <param name="documentName">Tên tài liệu để gán metadata</param>
    IReadOnlyList<TextChunkItem> ChunkText(string rawText, int chunkSize = 600, int overlapSize = 100, string? documentName = null);
}
