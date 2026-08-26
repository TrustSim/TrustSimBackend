using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimCategoryMappingConfiguration : IEntityTypeConfiguration<SimCategoryMapping>
{
    public void Configure(EntityTypeBuilder<SimCategoryMapping> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScoreContribution).IsRequired().HasDefaultValue(1.0f);

        builder.HasOne(x => x.SimNumber)
               .WithMany(s => s.SimCategoryMappings)
               .HasForeignKey(x => x.SimNumberId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SimCategory)
               .WithMany(c => c.SimCategoryMappings)
               .HasForeignKey(x => x.SimCategoryId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SimNumberId, x.SimCategoryId }).IsUnique();
    }
}
