using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class SimTransferConfiguration : IEntityTypeConfiguration<SimTransfer>
{
    public void Configure(EntityTypeBuilder<SimTransfer> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SimTransferStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.CancelledReason).HasMaxLength(500);

        builder.HasOne(x => x.TradeTransaction)
               .WithOne(t => t.SimTransfer)
               .HasForeignKey<SimTransfer>(x => x.TradeTransactionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TradeTransactionId).IsUnique();
        builder.HasIndex(x => x.SimTransferStatus);
    }
}
