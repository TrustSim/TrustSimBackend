using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Sim;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Sim;

public class SimNumberConfiguration : IEntityTypeConfiguration<SimNumber>
{
    public void Configure(EntityTypeBuilder<SimNumber> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(15);

        builder.Property(x => x.SimStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.HasOne(x => x.PhonePrefix)
               .WithMany(p => p.SimNumbers)
               .HasForeignKey(x => x.PhonePrefixId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FormatRhythm)
               .WithMany(f => f.SimNumbers)
               .HasForeignKey(x => x.FormatRhythmId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.HasIndex(x => x.SimStatus);
        builder.HasIndex(x => x.PhonePrefixId);
        builder.HasIndex(x => x.FormatRhythmId);
    }
}
