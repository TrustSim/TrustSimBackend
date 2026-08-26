using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionDepositConfiguration : IEntityTypeConfiguration<AuctionDeposit>
{
    public void Configure(EntityTypeBuilder<AuctionDeposit> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DepositCode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DepositAmount).HasPrecision(18, 2);

        builder.Property(x => x.DepositStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.ForfeitedReason).HasMaxLength(500);

        builder.HasOne(x => x.AuctionParticipant)
               .WithMany(p => p.Deposits)
               .HasForeignKey(x => x.ParticipantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.AuctionTransaction)
               .WithOne(t => t.AuctionDeposit)
               .HasForeignKey<AuctionDeposit>(x => x.AuctionTransactionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DepositCode).IsUnique();
        builder.HasIndex(x => x.ParticipantId);
        builder.HasIndex(x => x.AuctionTransactionId);
        builder.HasIndex(x => x.DepositStatus);
    }
}
