using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionParticipantConfiguration : IEntityTypeConfiguration<AuctionParticipant>
{
    public void Configure(EntityTypeBuilder<AuctionParticipant> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.RejectReason).HasMaxLength(500);
        builder.Property(x => x.BlockedReason).HasMaxLength(500);

        builder.HasOne(x => x.Auction)
               .WithMany(a => a.Participants)
               .HasForeignKey(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.UserAccount)
               .WithMany(u => u.AuctionParticipants)
               .HasForeignKey(x => x.UserAccountId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.AuctionId, x.UserAccountId }).IsUnique();
        builder.HasIndex(x => x.Status);
    }
}
