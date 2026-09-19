using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AccountType)
               .IsRequired()
               .HasConversion<int>();

        builder.Property(x => x.EntryType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(20);

        builder.Property(x => x.Amount)
               .HasPrecision(18, 2);

        builder.Property(x => x.SequenceNumber)
               .IsRequired();

        builder.Property(x => x.PrevHash)
               .IsRequired()
               .HasMaxLength(128);

        builder.Property(x => x.EntryHash)
               .IsRequired()
               .HasMaxLength(128);

        builder.Property(x => x.Signature)
               .IsRequired()
               .HasMaxLength(512);

        builder.Property(x => x.Description)
               .HasMaxLength(500);

        builder.HasOne(x => x.Wallet)
               .WithMany()
               .HasForeignKey(x => x.WalletId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SequenceNumber).IsUnique();
        builder.HasIndex(x => x.EntryHash).IsUnique();
        builder.HasIndex(x => x.JournalId);
        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.AccountType);
        builder.HasIndex(x => x.CreatedAt);
    }
}
