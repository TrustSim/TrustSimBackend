namespace TrustSimBackend.Application.Common.Interfaces;

/// <summary>
/// Result of a semantic vector similarity cache lookup.
/// </summary>
public sealed record SemanticCacheMatch<T>(
    bool Found,
    T? Value,
    double Similarity,
    double BestCandidateSimilarity,
    string? MatchedPrompt,
    string CacheLayer);

/// <summary>
/// Two-tier AI cache abstraction (L1 in-memory + L2 Redis with automatic fallback) supporting both exact-hash and semantic similarity lookup.
/// </summary>
public interface IAiCacheService
{
    Task<(bool Found, T? Value, string CacheLayer)> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default);
    string BuildHashKey(string prefix, params string?[] parts);

    Task<SemanticCacheMatch<T>> FindSemanticMatchAsync<T>(
        string scopeKey,
        ReadOnlyMemory<float> queryEmbedding,
        double similarityThreshold,
        CancellationToken cancellationToken = default);

    Task SetSemanticEntryAsync<T>(
        string scopeKey,
        string promptText,
        ReadOnlyMemory<float> embedding,
        T value,
        TimeSpan ttl,
        int maxEntriesPerScope = 50,
        CancellationToken cancellationToken = default);
}
