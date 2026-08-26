using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TransferContractConfiguration : IEntityTypeConfiguration<TransferContract>
{
    public void Configure(EntityTypeBuilder<TransferContract> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.DocumentHash).IsRequired().HasMaxLength(256);
        builder.Property(x => x.BlockchainTxHash).HasMaxLength(256);

        builder.Property(x => x.ContractStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne(x => x.TradeTransaction)
               .WithOne(t => t.TransferContract)
               .HasForeignKey<TransferContract>(x => x.TradeTransactionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TradeTransactionId).IsUnique();
        builder.HasIndex(x => x.ContractStatus);
    }
}
