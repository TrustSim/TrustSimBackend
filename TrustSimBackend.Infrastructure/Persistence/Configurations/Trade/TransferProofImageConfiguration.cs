using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TransferProofImageConfiguration : IEntityTypeConfiguration<TransferProofImage>
{
    public void Configure(EntityTypeBuilder<TransferProofImage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileName).HasMaxLength(255);
        builder.Property(x => x.ContentType).HasMaxLength(100);

        builder.HasOne(x => x.TransferProof)
               .WithMany(p => p.Images)
               .HasForeignKey(x => x.TransferProofId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TransferProofId);
    }
}
