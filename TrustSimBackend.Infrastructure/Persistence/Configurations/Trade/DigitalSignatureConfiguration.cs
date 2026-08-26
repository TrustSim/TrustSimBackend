using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class DigitalSignatureConfiguration : IEntityTypeConfiguration<DigitalSignature>
{
    public void Configure(EntityTypeBuilder<DigitalSignature> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SignatureRole)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.PublicKey).IsRequired();
        builder.Property(x => x.CryptographicSignature).IsRequired();

        builder.HasOne(x => x.Contract)
               .WithMany()
               .HasForeignKey(x => x.ContracId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
               .WithMany(u => u.DigitalSignatures)
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ContracId);
        builder.HasIndex(x => x.UserId);
    }
}
