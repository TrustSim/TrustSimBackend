using Microsoft.EntityFrameworkCore;
using TrustSimBackend.Domain.Entities.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity
{
    public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            builder.ToTable("user_account");

            builder.HasKey(u => u.Id);

            builder.Property(u => u.UserName).IsRequired().HasMaxLength(50);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
            builder.Property(u => u.IsActive).HasDefaultValue(true);

            // Bỏ qua các alias properties để tránh xung đột với Identity columns
            builder.Ignore(u => u.UserEmail);
            builder.Ignore(u => u.PassWordHash);
        }
    }
}