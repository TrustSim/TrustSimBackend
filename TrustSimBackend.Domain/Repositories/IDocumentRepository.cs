using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Domain.Repositories;

/// <summary>
/// Repository Interface quản lý Văn bản / Điều khoản (Document).
/// </summary>
public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Document document, CancellationToken cancellationToken = default);
    void Update(Document document);
}
