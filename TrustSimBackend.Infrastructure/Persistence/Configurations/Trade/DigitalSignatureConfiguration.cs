using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class DigitalSignatureConfiguration : IEntityTypeConfiguration<DigitalSignature>
{
    public void Configure(EntityTypeBuilder<DigitalSignature> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SignatureRole)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.AuthMethod).IsRequired().HasMaxLength(50);
        builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(50);
        builder.Property(x => x.UserAgent).IsRequired().HasMaxLength(500);
        builder.Property(x => x.OtpCodeHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.AuditMetadataJson).HasColumnType("jsonb");

        builder.Property(x => x.PublicKey).HasMaxLength(500);
        builder.Property(x => x.CryptographicSignature).HasMaxLength(1000);

        builder.HasOne(x => x.TransferContract)
               .WithMany(c => c.DigitalSignatures)
               .HasForeignKey(x => x.TransferContractId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
               .WithMany(u => u.DigitalSignatures)
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        // Chặn Race Condition: 1 Hợp đồng chỉ được phép có DUY NHẤT 1 chữ ký cho mỗi vai trò (BUYER / SELLER / PLATFORM_ARBITRATOR)
        builder.HasIndex(x => new { x.TransferContractId, x.SignatureRole }).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
