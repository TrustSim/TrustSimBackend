using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Auctions;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Auction;

public class FraudAssessmentConfiguration : IEntityTypeConfiguration<FraudAssessment>
{
    public void Configure(EntityTypeBuilder<FraudAssessment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RiskLevel)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(30);

        builder.Property(x => x.AssessmentDecision)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.AssessmentStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.FraudSignal).HasMaxLength(500);

        builder.HasOne(x => x.AuctionParticipant)
               .WithMany(p => p.FraudAssessments)
               .HasForeignKey(x => x.ParticipantId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ParticipantId);
        builder.HasIndex(x => x.RiskLevel);
        builder.HasIndex(x => x.AssessmentStatus);
    }
}
