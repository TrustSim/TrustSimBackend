using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class LedgerJournalConfiguration : IEntityTypeConfiguration<LedgerJournal>
{
    public void Configure(EntityTypeBuilder<LedgerJournal> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JournalCode)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(x => x.TransactionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.ReferenceType)
               .HasMaxLength(100);

        builder.Property(x => x.IdempotencyKey)
               .HasMaxLength(200);

        builder.Property(x => x.Description)
               .HasMaxLength(500);

        builder.Property(x => x.TotalAmount)
               .HasPrecision(18, 2);

        builder.Property(x => x.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.SignedHash)
               .HasMaxLength(512);

        builder.HasMany(x => x.Entries)
               .WithOne(e => e.Journal)
               .HasForeignKey(e => e.JournalId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.JournalCode).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey);
        builder.HasIndex(x => x.TransactionType);
        builder.HasIndex(x => x.PostedAt);
    }
}
