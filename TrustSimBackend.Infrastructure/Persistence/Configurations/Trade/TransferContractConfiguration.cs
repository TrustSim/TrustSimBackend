using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TransferContractConfiguration : IEntityTypeConfiguration<TransferContract>
{
    public void Configure(EntityTypeBuilder<TransferContract> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.DocumentUrl).IsRequired(false).HasMaxLength(500);
        builder.Property(x => x.DocumentHash).IsRequired(false).HasMaxLength(256);
        builder.Property(x => x.BlockchainTxHash).IsRequired(false).HasMaxLength(256);

        builder.Property(x => x.PadesSignatureSerial).HasMaxLength(100);
        builder.Property(x => x.PadesSignatureHash).HasMaxLength(256);
        builder.Property(x => x.TsaAuthority).HasMaxLength(150);
        builder.Property(x => x.IsAutoEnforced).HasDefaultValue(false).IsRequired();

        builder.Property(x => x.ContractStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne(x => x.TradeTransaction)
               .WithOne(t => t.TransferContract)
               .HasForeignKey<TransferContract>(x => x.TradeTransactionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.DigitalSignatures)
               .WithOne(s => s.TransferContract)
               .HasForeignKey(s => s.TransferContractId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TradeTransactionId).IsUnique();
        builder.HasIndex(x => x.ContractStatus);
    }
}
