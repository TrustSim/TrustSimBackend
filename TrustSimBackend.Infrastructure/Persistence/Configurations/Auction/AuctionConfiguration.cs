using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class AuctionConfiguration : IEntityTypeConfiguration<Domain.Entities.Auctions.Auction>
{
    public void Configure(EntityTypeBuilder<Domain.Entities.Auctions.Auction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuctionCode).IsRequired().HasMaxLength(50);

        builder.Property(x => x.AuctionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.AuctionStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.StartingPrice).HasPrecision(18, 2);
        builder.Property(x => x.CurrentPrice).HasPrecision(18, 2);
        builder.Property(x => x.ReservePrice).HasPrecision(18, 2);

        builder.Property(x => x.LockVersion).IsConcurrencyToken();

        builder.HasOne(x => x.Author)
               .WithMany(u => u.Auctions)
               .HasForeignKey(x => x.AuthorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SimNumber)
               .WithMany()
               .HasForeignKey(x => x.SimNumberId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AuctionCode).IsUnique();
        builder.HasIndex(x => x.AuthorId);
        builder.HasIndex(x => x.SimNumberId);
        builder.HasIndex(x => x.AuctionStatus);
        builder.HasIndex(x => x.StartAt);
        builder.HasIndex(x => x.EndAt);
    }
}
