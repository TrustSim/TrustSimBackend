using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AvailableBalance).HasPrecision(18, 2).HasDefaultValue(0);
        builder.Property(x => x.LockedBalance).HasPrecision(18, 2).HasDefaultValue(0);

        builder.Property(x => x.WalletStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.LockVersion).IsConcurrencyToken();

        builder.HasOne(x => x.UserAccount)
               .WithOne(u => u.Wallet)
               .HasForeignKey<Wallet>(x => x.UserAccountId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.UserAccountId).IsUnique();
    }
}
