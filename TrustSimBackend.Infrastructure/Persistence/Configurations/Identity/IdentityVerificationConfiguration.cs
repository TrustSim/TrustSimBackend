using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity;

public class IdentityVerificationConfiguration : IEntityTypeConfiguration<IdentityVerification>
{
    public void Configure(EntityTypeBuilder<IdentityVerification> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.VerificationStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.SubmittedAt).IsRequired();

        builder.Property(x => x.FrontIdCardUrl).HasMaxLength(500);
        builder.Property(x => x.BackIdCardUrl).HasMaxLength(500);
        builder.Property(x => x.FaceSelfieUrl).HasMaxLength(500);
        builder.Property(x => x.FaceSimilarityScore).HasDefaultValue(0.0f);
        builder.Property(x => x.OcrRawData).HasColumnType("text");
        builder.Property(x => x.VerificationMethod).HasMaxLength(50).HasDefaultValue("AUTOMATED_AWS_REKOGNITION");

        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasOne(x => x.UserProfile)
               .WithMany(p => p.IdentityVerifications)
               .HasForeignKey(x => x.UserProfileId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.VerifiedByUser)
               .WithMany()
               .HasForeignKey(x => x.VerifiedByUserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserProfileId);
        builder.HasIndex(x => x.VerificationStatus);
    }
}

