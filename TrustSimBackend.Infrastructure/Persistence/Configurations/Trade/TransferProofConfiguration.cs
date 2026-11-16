using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TransferProofConfiguration : IEntityTypeConfiguration<TransferProof>
{
    public void Configure(EntityTypeBuilder<TransferProof> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.RejectionReason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);

        builder.Property(x => x.ProofStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.ProofType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne(x => x.Auction)
               .WithMany(a => a.TransferProofs)
               .HasForeignKey(x => x.AuctionId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.SimTransfer)
               .WithMany(s => s.TransferProofs)
               .HasForeignKey(x => x.SimTransferId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.SubmittedByUser)
               .WithMany()
               .HasForeignKey(x => x.SubmittedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.VerifiedByAdmin)
               .WithMany()
               .HasForeignKey(x => x.VerifiedByAdminId)
               .IsRequired(false)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AuctionId);
        builder.HasIndex(x => x.SimTransferId);
        builder.HasIndex(x => new { x.AuctionId, x.ProofType, x.ProofStatus });

        // Mỗi phiên đấu giá và loại bằng chứng chỉ được phép có tối đa 1 hồ sơ PENDING tại một thời điểm
        builder.HasIndex(x => new { x.AuctionId, x.ProofType })
               .IsUnique()
               .HasFilter("proof_status = 'PENDING'");

        // Chống gửi trùng bằng Idempotency Key trong phạm vi phiên đấu giá
        builder.HasIndex(x => new { x.AuctionId, x.IdempotencyKey })
               .IsUnique()
               .HasFilter("idempotency_key IS NOT NULL");
    }
}
