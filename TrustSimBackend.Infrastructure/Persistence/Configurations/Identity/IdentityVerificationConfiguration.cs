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

        builder.Property(x => x.RejectionReason).HasMaxLength(500);

        builder.HasOne(x => x.UserProfile)
               .WithMany(p => p.IdentityVerifications)
               .HasForeignKey(x => x.UserProfileId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.UserProfileId);
    }
}
