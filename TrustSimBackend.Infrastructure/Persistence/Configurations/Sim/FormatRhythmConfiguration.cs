using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class FormatRhythmConfiguration : IEntityTypeConfiguration<FormatRhythm>
{
    public void Configure(EntityTypeBuilder<FormatRhythm> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Value).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FormatRhythmName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasIndex(x => x.FormatRhythmName);
    }
}
