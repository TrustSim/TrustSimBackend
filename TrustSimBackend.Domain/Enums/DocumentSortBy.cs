namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Các trường hỗ trợ sắp xếp danh sách tài liệu hệ thống / tài liệu RAG.
/// </summary>
public enum DocumentSortBy
{
    CreatedDate = 1,
    DocumentName = 2,
    TotalChunks = 3,
    TotalTokens = 4,
    Status = 5
}
