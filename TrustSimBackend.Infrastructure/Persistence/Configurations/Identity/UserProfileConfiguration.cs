using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity
{
    public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
        

            builder.HasKey(p=>p.Id);

           builder.Property(p=>p.FullName).IsRequired().HasMaxLength(150);

           builder.Property(p=>p.DateOfBirth).IsRequired();

           builder.Property(p=>p.Email).IsRequired().HasMaxLength(50);

           builder.Property(p=>p.PhoneNumber).IsRequired().HasMaxLength(11);

           builder.Property(p=>p.Address).IsRequired().HasMaxLength(200);

           builder.Property(p=>p.IdentityNumber).IsRequired().HasMaxLength(12);

           builder.Property(p=>p.AvatarImg).IsRequired();

           builder.Property(p=>p.VerificationStatus).IsRequired().HasConversion<string>();

           builder.HasOne(p=>p.UserAccount)
                .WithOne(u=>u.UserProfile)
                .HasForeignKey<UserProfile>(p=>p.UserAccountId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(p=>p.IdentityVerifications)
                .WithOne(i=>i.UserProfile)
                .HasForeignKey(i=>i.UserProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            // index
            builder.HasIndex(p=>p.PhoneNumber).IsUnique();
            builder.HasIndex(p=>p.IdentityNumber).IsUnique();
            



        }
    }
}