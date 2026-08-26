using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Dispute;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Dispute;

public class DisputeProofConfiguration : IEntityTypeConfiguration<DisputeProof>
{
    public void Configure(EntityTypeBuilder<DisputeProof> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasOne(x => x.DisputeTicket)
               .WithMany(t => t.Proofs)
               .HasForeignKey(x => x.DisputeTicketId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DisputeTicketId);
    }
}
