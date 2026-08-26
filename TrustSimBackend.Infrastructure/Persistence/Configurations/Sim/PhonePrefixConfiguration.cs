using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class PhonePrefixConfiguration : IEntityTypeConfiguration<PhonePrefix>
{
    public void Configure(EntityTypeBuilder<PhonePrefix> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PrefixValue).IsRequired().HasMaxLength(10);

        builder.HasOne(x => x.SimCarrier)
               .WithMany(c => c.PhonePrefixes)
               .HasForeignKey(x => x.SimCarrierId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PrefixValue).IsUnique();
        builder.HasIndex(x => x.SimCarrierId);
    }
}
