using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionRuleConfiguration : IEntityTypeConfiguration<AuctionRule>
{
    public void Configure(EntityTypeBuilder<AuctionRule> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.MinimumBidIncrement).HasPrecision(18, 2);
        builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
        builder.Property(x => x.DutchDecreaseAmount).HasPrecision(18, 2);

        builder.Property(x => x.MaxStepMultiplier).HasDefaultValue(10).IsRequired();
        builder.Property(x => x.MaxSnipeExtensionsPerParticipant).HasDefaultValue(3).IsRequired();
        builder.Property(x => x.PaymentGracePeriodHours).HasDefaultValue(24).IsRequired();

        builder.Property(x => x.MarginPercentage).HasPrecision(5, 2).HasDefaultValue(0.10m).IsRequired();
        builder.Property(x => x.MaxPriceGapRatioForRunnerUp).HasPrecision(5, 2).HasDefaultValue(1.50m).IsRequired();
        builder.Property(x => x.MinimumUniqueBiddersForRunnerUp).HasDefaultValue(2).IsRequired();
        builder.Property(x => x.RequireSellerApprovalForRunnerUp).HasDefaultValue(true).IsRequired();

        builder.Property(x => x.SellerDepositAmount).HasPrecision(18, 2).HasDefaultValue(0m).IsRequired();
        builder.Property(x => x.SellerDepositRate).HasPrecision(5, 4).HasDefaultValue(0.05m).IsRequired();
        builder.Property(x => x.SigningGracePeriodHours).HasDefaultValue(24).IsRequired();
        builder.Property(x => x.SigningGracePeriodMinutes).HasDefaultValue(1440).IsRequired();

        builder.HasOne(x => x.Auction)
               .WithOne(a => a.AuctionRule)
               .HasForeignKey<AuctionRule>(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AuctionId).IsUnique();
    }
}
