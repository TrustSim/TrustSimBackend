using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Domain.Repositories;

namespace TrustSimBackend.Infrastructure.Persistence.Repositories;

public class DocumentChunkRepository : IDocumentChunkRepository
{
    private readonly TrustSimDbContext _context;

    public DocumentChunkRepository(TrustSimDbContext context)
    {
        _context = context;
    }

    public async Task<DocumentChunk?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentChunks
            .Include(c => c.Document)
            .FirstOrDefaultAsync(c => c.Id == id && c.IsActive, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId && c.IsActive)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        await _context.DocumentChunks.AddRangeAsync(chunks, cancellationToken);
    }

    public async Task DeleteByDocumentIdAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var chunks = await _context.DocumentChunks
            .Where(c => c.DocumentId == documentId)
            .ToListAsync(cancellationToken);

        _context.DocumentChunks.RemoveRange(chunks);
    }

    public async Task<IReadOnlyList<(DocumentChunk Chunk, double Similarity)>> SearchSimilarChunksAsync(
        Vector queryEmbedding,
        int topK = 3,
        double minSimilarity = 0.4,
        CancellationToken cancellationToken = default)
    {
        // Sử dụng CosineDistance từ Pgvector.EntityFrameworkCore
        // Similarity = 1 - CosineDistance
        var query = _context.DocumentChunks
            .Include(c => c.Document)
            .Where(c => c.IsActive && c.Embedding != null);

        var topMatches = await query
            .Select(c => new
            {
                Chunk = c,
                Distance = c.Embedding!.CosineDistance(queryEmbedding)
            })
            .OrderBy(x => x.Distance)
            .Take(topK * 2) // Lấy dư một ít để lọc theo minSimilarity
            .ToListAsync(cancellationToken);

        var results = topMatches
            .Select(x => (Chunk: x.Chunk, Similarity: 1.0 - x.Distance))
            .Where(x => x.Similarity >= minSimilarity)
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();

        return results;
    }
}
