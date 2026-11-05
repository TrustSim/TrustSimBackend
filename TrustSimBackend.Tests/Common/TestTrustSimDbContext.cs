using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Ai;
using TrustSimBackend.Infrastructure.Persistence;

namespace TrustSimBackend.Tests.Common;

public class TestTrustSimDbContext : TrustSimDbContext
{
    public TestTrustSimDbContext(DbContextOptions<TrustSimDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<DocumentChunk>().Ignore(x => x.Embedding);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Không gọi UseSnakeCaseNamingConvention hay Relational Interceptors khi chạy InMemory
    }

    public static TestTrustSimDbContext CreateInMemory(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<TrustSimDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString("N"))
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestTrustSimDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
