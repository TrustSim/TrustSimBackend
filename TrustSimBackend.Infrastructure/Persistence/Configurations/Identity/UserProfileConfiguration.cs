using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity
{
    public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
        

            builder.HasKey(p => p.Id);

            builder.Property(p => p.FullName).HasMaxLength(150).HasDefaultValue(string.Empty);

            builder.Property(p => p.DateOfBirth).IsRequired(false);

            builder.Property(p => p.Email).HasMaxLength(150).HasDefaultValue(string.Empty);

            builder.Property(p => p.PhoneNumber).HasMaxLength(20).HasDefaultValue(string.Empty);

            builder.Property(p => p.Address).HasMaxLength(200).HasDefaultValue(string.Empty);

            builder.Property(p => p.IdentityNumber).HasMaxLength(20).HasDefaultValue(string.Empty);

            builder.Property(p => p.AvatarImg).HasMaxLength(500).HasDefaultValue(string.Empty);

            builder.Property(p => p.VerificationStatus).IsRequired().HasConversion<string>();

            builder.HasOne(p => p.UserAccount)
                 .WithOne(u => u.UserProfile)
                 .HasForeignKey<UserProfile>(p => p.UserAccountId)
                 .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p => p.IdentityVerifications)
                 .WithOne(i => i.UserProfile)
                 .HasForeignKey(i => i.UserProfileId)
                 .OnDelete(DeleteBehavior.Cascade);

            // Filtered Unique Indexes (chỉ unique khi có giá trị khác rỗng)
            builder.HasIndex(p => p.PhoneNumber)
                .IsUnique()
                .HasFilter("phone_number IS NOT NULL AND phone_number <> ''");

            builder.HasIndex(p => p.IdentityNumber)
                .IsUnique()
                .HasFilter("identity_number IS NOT NULL AND identity_number <> ''");
            



        }
    }
}