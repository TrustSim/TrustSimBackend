using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimCarrierConfiguration : IEntityTypeConfiguration<SimCarrier>
{
    public void Configure(EntityTypeBuilder<SimCarrier> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CarrierName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.CarrierCode).IsRequired().HasMaxLength(20);
        builder.Property(x => x.LogoImg).HasMaxLength(500);

        builder.HasIndex(x => x.CarrierCode).IsUnique();
    }
}
