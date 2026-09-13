using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Domain.Entities.System;

/// <summary>
/// Đại diện cho một tài liệu hệ thống hoặc tài liệu tri thức nghiên cứu thị trường phục vụ RAG
/// </summary>
public class Document : FullAuditableEntity
{
    /// <summary>Tên hiển thị của tài liệu.</summary>
    public string DocumentName { get; set; } = string.Empty;

    /// <summary>Đường dẫn URL gốc của tài liệu để cào dữ liệu.</summary>
    public string LinkUrl { get; set; } = string.Empty;

    /// <summary>Phân loại mục đích tài liệu: MARKET_RESEARCH, FENG_SHUI_RULE, PRICING_GUIDELINE, GENERAL.</summary>
    public DocumentType DocumentType { get; set; } = DocumentType.GENERAL;

    /// <summary>Trạng thái xử lý ETL/RAG: PENDING, CRAWLING, PROCESSING, INDEXED, FAILED.</summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.PENDING;

    /// <summary>Nội dung văn bản thô đã được làm sạch sau khi cào từ URL.</summary>
    public string? RawContent { get; set; }

    /// <summary>Tổng số đoạn trích (Chunks) đã được tạo ra từ tài liệu này.</summary>
    public int TotalChunks { get; set; } = 0;

    /// <summary>Tổng số lượng token ước tính trong tài liệu.</summary>
    public int TotalTokens { get; set; } = 0;

    /// <summary>Thời điểm hoàn tất cào và đánh chỉ mục vector thành công.</summary>
    public DateTime? CrawledAt { get; set; }

    /// <summary>Thông điệp lỗi nếu quá trình cào / xử lý vector thất bại.</summary>
    public string? ErrorMessage { get; set; }

    // --- Navigation Properties ---
    /// <summary>Danh sách các đoạn văn bản (Chunks) và Vector Embeddings của tài liệu.</summary>
    public virtual ICollection<DocumentChunk> Chunks { get; set; } = new List<DocumentChunk>();
}