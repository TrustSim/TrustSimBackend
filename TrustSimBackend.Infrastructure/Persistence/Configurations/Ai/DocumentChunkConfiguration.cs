using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Content).IsRequired();
        builder.Property(x => x.ChunkIndex).IsRequired();
        builder.Property(x => x.TokenCount).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        // Cấu hình kiểu dữ liệu vector 1024 chiều từ pgvector
        builder.Property(x => x.Embedding)
            .HasColumnType("vector(1024)");

        // Quan hệ với Document
        builder.HasOne(x => x.Document)
            .WithMany(d => d.Chunks)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Chỉ mục HNSW cho vector cosine distance (hoặc index tra cứu document)
        builder.HasIndex(x => x.DocumentId);
    }
}
