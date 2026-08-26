using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class EscrowConfiguration : IEntityTypeConfiguration<Escrow>
{
    public void Configure(EntityTypeBuilder<Escrow> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EscrowAmount).HasPrecision(18, 2);

        builder.Property(x => x.EscrowStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.FrozenReason).HasMaxLength(500);

        builder.HasOne(x => x.TradeTransaction)
               .WithOne(t => t.Escrow)
               .HasForeignKey<Escrow>(x => x.TradeTransactionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.AuctionDeposit)
               .WithMany()
               .HasForeignKey(x => x.AuctionDepositId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.TradeTransactionId).IsUnique();
        builder.HasIndex(x => x.AuctionDepositId);
        builder.HasIndex(x => x.EscrowStatus);
    }
}
