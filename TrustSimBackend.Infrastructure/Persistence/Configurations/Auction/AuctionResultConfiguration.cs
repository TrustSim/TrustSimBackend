using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionResultConfiguration : IEntityTypeConfiguration<AuctionResult>
{
    public void Configure(EntityTypeBuilder<AuctionResult> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.FinalPrice).HasPrecision(18, 2);

        builder.Property(x => x.AuctionResultStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.CancelledReason).HasMaxLength(500);

        builder.HasOne(x => x.Auction)
               .WithOne()
               .HasForeignKey<AuctionResult>(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Winner)
               .WithMany()
               .HasForeignKey(x => x.WinnerId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AuctionId).IsUnique();
        builder.HasIndex(x => x.WinnerId);
    }
}
