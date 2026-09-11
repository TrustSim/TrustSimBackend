using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.TransactionCode)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(x => x.ExternalTransactionId)
               .HasMaxLength(100);

        builder.Property(x => x.QrCodeUrl)
               .HasMaxLength(1000);

        builder.Property(x => x.Description)
               .HasMaxLength(500);

        builder.Property(x => x.TransactionStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.WalletTransactionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceBefore).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2);

        builder.Property(x => x.DestinationBankCode).HasMaxLength(50);
        builder.Property(x => x.DestinationBankName).HasMaxLength(150);
        builder.Property(x => x.DestinationAccountNumber).HasMaxLength(50);
        builder.Property(x => x.DestinationAccountNumberMasked).HasMaxLength(50);
        builder.Property(x => x.DestinationAccountHolderName).HasMaxLength(150);

        builder.HasOne(x => x.Wallet)
               .WithMany(w => w.WalletTransactions)
               .HasForeignKey(x => x.WalletId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.TransactionCode);
        builder.HasIndex(x => x.TransactionStatus);
        builder.HasIndex(x => x.WalletTransactionType);
    }
}
