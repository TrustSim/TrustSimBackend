using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimCategoryConfiguration : IEntityTypeConfiguration<SimCategory>
{
    public void Configure(EntityTypeBuilder<SimCategory> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.CategoryCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CategoryName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.ValuationMultiplier).IsRequired().HasDefaultValue(1.0f);

        builder.HasIndex(x => x.CategoryCode).IsUnique();
    }
}
