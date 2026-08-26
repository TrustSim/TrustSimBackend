using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity
{
    public class UserSecurityProfileConfiguration : IEntityTypeConfiguration<UserSecurityProfile>
    {
        public void Configure(EntityTypeBuilder<UserSecurityProfile> builder)
        {

            builder.HasKey(u=>u.Id);

            builder.HasOne(s=>s.UserAccount).WithOne(a=>a.UserSecurityProfile)
                    .HasForeignKey<UserSecurityProfile>(s=>s.UserAccountId)
                    .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(u=>new { u.UserAccountId, u.UserSecurityProfileStatus}).IsUnique();

            builder.Property(u=>u.KeyHash).IsRequired().HasMaxLength(250);
            builder.Property(u=>u.PublicKey).IsRequired().HasMaxLength(250);
            builder.Property(u=>u.EncryptedPrivateKey).IsRequired().HasMaxLength(250);

            builder.Property(u=>u.KeyGeneratedAt).IsRequired();

            builder.Property(u=>u.UserSecurityProfileStatus).IsRequired().HasConversion<string>().HasMaxLength(50);

            
            

            
            
            
        }
    }
}