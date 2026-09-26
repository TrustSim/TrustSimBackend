using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.BidAmount).HasPrecision(18, 2);

        builder.Property(x => x.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.RejectedReason).HasMaxLength(500);

        builder.HasOne(x => x.Auction)
               .WithMany(a => a.Bids)
               .HasForeignKey(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Participant)
               .WithMany(p => p.Bids)
               .HasForeignKey(x => x.ParticipantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.AuctionId);
        builder.HasIndex(x => x.ParticipantId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PlacedAt);
        builder.HasIndex(x => new { x.AuctionId, x.BidAmount });
    }
}
