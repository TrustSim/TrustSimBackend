using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Filters;

/// <summary>
/// Bộ lọc tra cứu danh sách tài liệu quy chế / tài liệu RAG.
/// </summary>
public class DocumentQueryFilter : BaseQueryFilter<DocumentSortBy>
{
    /// <summary>
    /// Trạng thái xử lý ETL/RAG (PENDING, CRAWLING, PROCESSING, INDEXED, FAILED).
    /// </summary>
    public DocumentStatus? Status { get; set; }

    /// <summary>
    /// Phân loại mục đích tài liệu (MARKET_RESEARCH, FENG_SHUI_RULE, PRICING_GUIDELINE, GENERAL).
    /// </summary>
    public DocumentType? DocumentType { get; set; }

    /// <summary>
    /// Chỉ lấy các tài liệu đang kích hoạt (mặc định null để lấy tất cả hoặc true/false).
    /// </summary>
    public bool? ActiveOnly { get; set; }
}
