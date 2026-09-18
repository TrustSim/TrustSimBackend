using Pgvector;
using TrustSimBackend.Domain.Base;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Domain.Entities.Ai;

/// <summary>
/// Đại diện cho một đoạn văn bản (Chunk) được bẻ nhỏ từ tài liệu và nhúng Vector phục vụ cho RAG Semantic Search.
/// </summary>
public class DocumentChunk : FullAuditableEntity
{
    /// <summary>ID của tài liệu nguồn (<see cref="Document"/>).</summary>
    public Guid DocumentId { get; set; }

    /// <summary>Thứ tự của đoạn trích trong tài liệu (bắt đầu từ 0 hoặc 1).</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Nội dung văn bản thô của đoạn trích.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Vector nhúng ngữ nghĩa (2048 chiều) sinh ra từ NVIDIA Embedding API (nvidia/nemotron-3-embed-1b).</summary>
    public Vector? Embedding { get; set; }

    /// <summary>Số lượng token ước tính của đoạn trích.</summary>
    public int TokenCount { get; set; }

    /// <summary>Metadata bổ sung dưới dạng JSON (tiêu đề, chương mục, tags, từ khóa liên quan).</summary>
    public string? MetadataJson { get; set; }

    // --- Navigation Property ---
    /// <summary>Tài liệu nguồn chứa đoạn trích này.</summary>
    public virtual Document? Document { get; set; }
}
