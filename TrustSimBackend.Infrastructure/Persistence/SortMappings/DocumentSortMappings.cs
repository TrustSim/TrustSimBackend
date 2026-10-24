using System.Linq.Expressions;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.SortMappings;

/// <summary>
/// Cấu hình Whitelist Mapping sắp xếp cho các truy vấn Danh sách tài liệu (Document).
/// </summary>
public static class DocumentSortMappings
{
    public static readonly IReadOnlyDictionary<DocumentSortBy, Expression<Func<Document, object>>> DocumentMappings =
        new Dictionary<DocumentSortBy, Expression<Func<Document, object>>>
        {
            [DocumentSortBy.CreatedDate] = d => d.CreatedDate,
            [DocumentSortBy.DocumentName] = d => d.DocumentName,
            [DocumentSortBy.TotalChunks] = d => d.TotalChunks,
            [DocumentSortBy.TotalTokens] = d => d.TotalTokens,
            [DocumentSortBy.Status] = d => d.Status
        };
}
