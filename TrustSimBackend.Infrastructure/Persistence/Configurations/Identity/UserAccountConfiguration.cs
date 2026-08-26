using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity
{
    public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            

            builder.HasKey(u=>u.Id);

            builder.Property(u=>u.UserName).IsRequired().HasMaxLength(50);

            builder.HasIndex(u=>u.UserName).IsUnique();

            builder.Property(u=>u.UserEmail).IsRequired().HasMaxLength(150);

            builder.HasIndex(u=>u.UserEmail).IsUnique();

            builder.Property(u=> u.PassWordHash).IsRequired();

            builder.Property(u=> u.Role).HasConversion<string>().HasMaxLength(20);

            
            

            
            
        }
    }
}