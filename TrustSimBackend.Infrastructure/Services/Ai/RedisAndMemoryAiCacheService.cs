using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TrustSimBackend.Application.Common.Interfaces;

namespace TrustSimBackend.Infrastructure.Services.Ai;

/// <summary>
/// Two-tier AI cache using L1 <see cref="IMemoryCache"/> and L2 Redis (<see cref="IConnectionMultiplexer"/>) with automatic fallback.
/// </summary>
public class RedisAndMemoryAiCacheService : IAiCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisAndMemoryAiCacheService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semanticBucketLocks = new(StringComparer.Ordinal);

    private sealed class SemanticBucketEntry<T>
    {
        public string Prompt { get; set; } = string.Empty;
        public float[] Embedding { get; set; } = Array.Empty<float>();
        public T? Value { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RedisAndMemoryAiCacheService(
        IMemoryCache memoryCache,
        ILogger<RedisAndMemoryAiCacheService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _memoryCache = memoryCache;
        _logger = logger;
        _redis = redis;
    }

    public async Task<(bool Found, T? Value, string CacheLayer)> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return (false, default, "MISS");
        }

        if (_memoryCache.TryGetValue(key, out T? l1Value) && l1Value != null)
        {
            return (true, l1Value, "L1_MEMORY");
        }

        if (_redis is not { IsConnected: true })
        {
            return (false, default, "MISS");
        }

        try
        {
            var db = _redis.GetDatabase();
            var raw = await db.StringGetAsync(key);
            if (!raw.HasValue)
            {
                return (false, default, "MISS");
            }

            var deserialized = JsonSerializer.Deserialize<T>(raw.ToString(), JsonOptions);
            if (deserialized == null)
            {
                return (false, default, "MISS");
            }

            // Backfill L1 for fast subsequent hits on the same instance.
            _memoryCache.Set(key, deserialized, TimeSpan.FromMinutes(5));
            return (true, deserialized, "L2_REDIS");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI Cache] Failed reading Redis key {Key}. Falling back to L1 memory only.", key);
            return (false, default, "MISS");
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key) || value == null || ttl <= TimeSpan.Zero)
        {
            return;
        }

        _memoryCache.Set(key, value, ttl);

        if (_redis is not { IsConnected: true })
        {
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, payload, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[AI Cache] Failed writing Redis key {Key}. L1 cache remains active.", key);
        }
    }

    public string BuildHashKey(string prefix, params string?[] parts)
    {
        var normalized = string.Join("|", parts.Select(NormalizePart));
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        var hex = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"trustsim:ai:{prefix}:{hex}";
    }

    public async Task<SemanticCacheMatch<T>> FindSemanticMatchAsync<T>(
        string scopeKey,
        ReadOnlyMemory<float> queryEmbedding,
        double similarityThreshold,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) || queryEmbedding.IsEmpty)
        {
            return new SemanticCacheMatch<T>(false, default, 0, 0, null, "MISS");
        }

        var (found, bucket, cacheLayer) = await GetAsync<List<SemanticBucketEntry<T>>>(scopeKey, cancellationToken);
        if (!found || bucket == null || bucket.Count == 0)
        {
            return new SemanticCacheMatch<T>(false, default, 0, 0, null, "MISS");
        }

        var (bestEntry, bestSimilarity) = FindBestSemanticEntry(bucket, queryEmbedding);

        if (bestEntry != null && bestSimilarity >= similarityThreshold)
        {
            return new SemanticCacheMatch<T>(
                true,
                bestEntry.Value,
                bestSimilarity,
                bestSimilarity,
                bestEntry.Prompt,
                cacheLayer);
        }

        return new SemanticCacheMatch<T>(false, default, 0, bestSimilarity, bestEntry?.Prompt, "MISS");
    }

    private static (SemanticBucketEntry<T>? BestEntry, double BestSimilarity) FindBestSemanticEntry<T>(
        List<SemanticBucketEntry<T>> bucket,
        ReadOnlyMemory<float> queryEmbedding)
    {
        var now = DateTimeOffset.UtcNow;
        var querySpan = queryEmbedding.Span;
        double bestSimilarity = 0;
        SemanticBucketEntry<T>? bestEntry = null;

        foreach (var entry in bucket)
        {
            if (entry.ExpiresAtUtc <= now || entry.Value == null || entry.Embedding.Length != querySpan.Length)
            {
                continue;
            }

            var similarity = ComputeCosineSimilarity(querySpan, entry.Embedding);
            if (similarity > bestSimilarity)
            {
                bestSimilarity = similarity;
                bestEntry = entry;
            }
        }

        return (bestEntry, bestSimilarity);
    }

    public async Task SetSemanticEntryAsync<T>(
        string scopeKey,
        string promptText,
        ReadOnlyMemory<float> embedding,
        T value,
        TimeSpan ttl,
        int maxEntriesPerScope = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scopeKey) || string.IsNullOrWhiteSpace(promptText) || embedding.IsEmpty || value == null || ttl <= TimeSpan.Zero)
        {
            return;
        }

        var gate = _semanticBucketLocks.GetOrAdd(scopeKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var (_, existing, _) = await GetAsync<List<SemanticBucketEntry<T>>>(scopeKey, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var normalizedPrompt = NormalizePart(promptText);

            var updated = (existing ?? new List<SemanticBucketEntry<T>>())
                .Where(e => e.ExpiresAtUtc > now && e.Value != null && !string.Equals(NormalizePart(e.Prompt), normalizedPrompt, StringComparison.Ordinal))
                .ToList();

            updated.Add(new SemanticBucketEntry<T>
            {
                Prompt = promptText.Trim(),
                Embedding = embedding.ToArray(),
                Value = value,
                ExpiresAtUtc = now.Add(ttl)
            });

            var cap = maxEntriesPerScope > 0 ? maxEntriesPerScope : 50;
            if (updated.Count > cap)
            {
                updated = updated.Skip(updated.Count - cap).ToList();
            }

            await SetAsync(scopeKey, updated, ttl, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }

    private static double ComputeCosineSimilarity(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length == 0 || a.Length != b.Length)
        {
            return 0;
        }

        double dot = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            double va = a[i];
            double vb = b[i];
            dot += va * vb;
            normA += va * va;
            normB += vb * vb;
        }

        var denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom <= 1e-12 ? 0 : dot / denom;
    }

    private static string NormalizePart(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var parts = raw.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}
