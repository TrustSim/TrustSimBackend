using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;
using TrustSimBackend.Domain.Enums;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionResultConfiguration : IEntityTypeConfiguration<AuctionResult>
{
    public void Configure(EntityTypeBuilder<AuctionResult> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinalPrice).HasPrecision(18, 2);
        builder.Property(x => x.InitialWinningPrice).HasPrecision(18, 2);
        builder.Property(x => x.RunnerUpFinalPrice).HasPrecision(18, 2);
        builder.Property(x => x.PriceGapRatio).HasPrecision(5, 2);
        builder.Property(x => x.SellerCompensationAmount).HasPrecision(18, 2);

        builder.Property(x => x.AuctionResultStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.WinType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50)
               .HasDefaultValue(AuctionWinType.NONE);

        builder.Property(x => x.CancelledReason).HasMaxLength(500);
        builder.Property(x => x.IsRunnerUpOffered).HasDefaultValue(false).IsRequired();

        builder.HasOne(x => x.Auction)
               .WithOne()
               .HasForeignKey<AuctionResult>(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Winner)
               .WithMany()
               .HasForeignKey(x => x.WinnerId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RunnerUpParticipant)
               .WithMany()
               .HasForeignKey(x => x.RunnerUpParticipantId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.InitialWinner)
               .WithMany()
               .HasForeignKey(x => x.InitialWinnerId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AuctionId).IsUnique();
        builder.HasIndex(x => x.WinnerId);
        builder.HasIndex(x => x.RunnerUpParticipantId);
        builder.HasIndex(x => x.InitialWinnerId);
    }
}
