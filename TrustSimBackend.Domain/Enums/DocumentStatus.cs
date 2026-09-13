namespace TrustSimBackend.Domain.Enums;

/// <summary>
/// Trạng thái xử lý ETL/RAG của tài liệu
/// </summary>
public enum DocumentStatus
{
    /// <summary>Chờ xử lý cào dữ liệu</summary>
    PENDING,

    /// <summary>Đang tải nội dung từ URL</summary>
    CRAWLING,

    /// <summary>Đang chia nhỏ văn bản và sinh vector nhúng (Embedding)</summary>
    PROCESSING,

    /// <summary>Đã đánh chỉ mục vector thành công, sẵn sàng phục vụ RAG</summary>
    INDEXED,

    /// <summary>Xử lý thất bại (lỗi kết nối, lỗi parse hoặc lỗi AI)</summary>
    FAILED
}
