using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimOwnershipConfiguration : IEntityTypeConfiguration<SimOwnership>
{
    public void Configure(EntityTypeBuilder<SimOwnership> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OwnershipStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.AcquistionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne(x => x.UserAccount)
               .WithMany(u => u.SimOwnerships)
               .HasForeignKey(x => x.UserAccountId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SimNumber)
               .WithMany(s => s.SimOwnerships)
               .HasForeignKey(x => x.SimNumberId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserAccountId);
        builder.HasIndex(x => x.SimNumberId);
        builder.HasIndex(x => x.OwnershipStatus);
    }
}
