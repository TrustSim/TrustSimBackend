using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Dispute;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Dispute;

public class DisputeTicketConfiguration : IEntityTypeConfiguration<DisputeTicket>
{
    public void Configure(EntityTypeBuilder<DisputeTicket> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Reason).IsRequired().HasMaxLength(500);

        builder.Property(x => x.Status)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.AdminResolutionNotes).HasMaxLength(1000);

        builder.HasOne(x => x.TradeTransaction)
               .WithMany(t => t.DisputeTickets)
               .HasForeignKey(x => x.TradeTransactionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Complainant)
               .WithMany()
               .HasForeignKey(x => x.ComplainantId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ResolvedByAdmin)
               .WithMany()
               .HasForeignKey(x => x.ResolvedByAdminId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.TradeTransactionId);
        builder.HasIndex(x => x.ComplainantId);
        builder.HasIndex(x => x.Status);
    }
}
