using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.System;
using TrustSimBackend.Domain.Enums;
using TrustSimBackend.Domain.Filters;
using TrustSimBackend.Domain.Repositories;
using TrustSimBackend.Infrastructure.Persistence.Extensions;
using TrustSimBackend.Infrastructure.Persistence.SortMappings;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class DocumentRepository : IDocumentRepository
{
    private readonly TrustSimDbContext _context;

    public DocumentRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = _context.Documents.Where(d => !d.IsDeleted);
        if (activeOnly)
        {
            query = query.Where(d => d.IsActive);
        }
        return await query.OrderBy(d => d.DocumentName).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Document> Items, int TotalCount)> GetPagedAsync(
        DocumentQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Documents.Where(d => !d.IsDeleted);

        if (filter.ActiveOnly.HasValue)
        {
            query = query.Where(d => d.IsActive == filter.ActiveOnly.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(d => d.Status == filter.Status.Value);
        }

        if (filter.DocumentType.HasValue)
        {
            query = query.Where(d => d.DocumentType == filter.DocumentType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var clean = filter.Keyword.Trim().ToLower();
            query = query.Where(d => d.DocumentName.ToLower().Contains(clean) || d.LinkUrl.ToLower().Contains(clean));
        }

        query = query.ApplyDateRange(d => d.CreatedDate, filter.FromDate, filter.ToDate);
        query = query.ApplySafeSorting(DocumentSortMappings.DocumentMappings, filter.SortBy, filter.SortOrder, defaultSortExpression: d => d.CreatedDate, defaultSortOrder: SortOrder.Desc);

        return await query.ToPagedTupleAsync(filter.PageIndex, filter.PageSize, cancellationToken);
    }

    public async Task<Document?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<Document?> GetByIdWithChunksAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .Include(d => d.Chunks.OrderBy(c => c.ChunkIndex))
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted, cancellationToken);
    }

    public async Task<IReadOnlyList<Document>> GetByStatusAsync(DocumentStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .Where(d => d.Status == status && !d.IsDeleted)
            .OrderByDescending(d => d.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Document document, CancellationToken cancellationToken = default)
    {
        await _context.Documents.AddAsync(document, cancellationToken);
    }

    public void Update(Document document)
    {
        _context.Documents.Update(document);
    }
}
