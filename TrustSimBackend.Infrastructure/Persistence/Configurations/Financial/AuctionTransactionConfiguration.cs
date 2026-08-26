using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class AuctionTransactionConfiguration : IEntityTypeConfiguration<AuctionTransaction>
{
    public void Configure(EntityTypeBuilder<AuctionTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.AuctionTransactionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceBefore).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2);

        builder.HasOne(x => x.Wallet)
               .WithMany(w => w.AuctionTransactions)
               .HasForeignKey(x => x.WalletId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Escrow)
               .WithMany(e => e.AuctionTransactions)
               .HasForeignKey(x => x.EscrowId)
               .OnDelete(DeleteBehavior.SetNull);


        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.EscrowId);
        builder.HasIndex(x => x.TransactionStatus);
    }
}
