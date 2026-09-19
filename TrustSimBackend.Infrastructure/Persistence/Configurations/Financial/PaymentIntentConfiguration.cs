using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Financial;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Financial;

public class PaymentIntentConfiguration : IEntityTypeConfiguration<PaymentIntent>
{
    public void Configure(EntityTypeBuilder<PaymentIntent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.IntentCode)
               .IsRequired()
               .HasMaxLength(100);

        builder.Property(x => x.IntentType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.Amount)
               .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
               .IsRequired()
               .HasMaxLength(10);

        builder.Property(x => x.IdempotencyKey)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(x => x.GatewayProvider)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(x => x.ExternalTransactionId)
               .HasMaxLength(100);

        builder.Property(x => x.QrCodeUrl)
               .HasMaxLength(1000);

        builder.Property(x => x.PaymentDetails)
               .HasMaxLength(1000);

        builder.Property(x => x.FailedReason)
               .HasMaxLength(500);

        builder.HasOne(x => x.UserAccount)
               .WithMany()
               .HasForeignKey(x => x.UserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Wallet)
               .WithMany()
               .HasForeignKey(x => x.WalletId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IntentCode).IsUnique();
        builder.HasIndex(x => x.IdempotencyKey);
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.WalletId);
        builder.HasIndex(x => x.Status);
    }
}
