using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimVerificationConfiguration : IEntityTypeConfiguration<SimVerification>
{
    public void Configure(EntityTypeBuilder<SimVerification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VerificationStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.RequestedAt).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasOne(x => x.SimOwnership)
               .WithMany(o => o.SimVerifications)
               .HasForeignKey(x => x.SimOwnershipId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SimOwnershipId);
    }
}
