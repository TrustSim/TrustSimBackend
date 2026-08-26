using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TradeTransactionConfiguration : IEntityTypeConfiguration<TradeTransaction>
{
    public void Configure(EntityTypeBuilder<TradeTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TradeTransactionCode).IsRequired().HasMaxLength(50);

        builder.Property(x => x.TradeTransactionStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.CancelledReason).HasMaxLength(500);

        builder.HasOne(x => x.Auction)
               .WithOne(a => a.TradeTransaction)
               .HasForeignKey<TradeTransaction>(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Buyer)
               .WithMany(u => u.BuyerTradeTransactions)
               .HasForeignKey(x => x.BuyerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Seller)
               .WithMany(u => u.SellerTradeTransactions)
               .HasForeignKey(x => x.SellerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SimNumber)
               .WithMany(s => s.TradeTransactions)
               .HasForeignKey(x => x.SimNumberId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TradeTransactionCode).IsUnique();
        builder.HasIndex(x => x.AuctionId);
        builder.HasIndex(x => x.BuyerId);
        builder.HasIndex(x => x.SellerId);
        builder.HasIndex(x => x.SimNumberId);
        builder.HasIndex(x => x.TradeTransactionStatus);
    }
}
