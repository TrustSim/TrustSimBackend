using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Identity;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Identity;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Token)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(rt => rt.JwtId)
            .HasMaxLength(128);

        builder.Property(rt => rt.CreatedByIp)
            .HasMaxLength(50);

        builder.Property(rt => rt.RevokedByIp)
            .HasMaxLength(50);

        builder.Property(rt => rt.ReplacedByToken)
            .HasMaxLength(256);

        builder.Property(rt => rt.ReasonRevoked)
            .HasMaxLength(255);

        // Đánh Index trên Token để tăng tốc độ tìm kiếm O(1) khi Refresh
        builder.HasIndex(rt => rt.Token)
            .IsUnique();

        // Đánh Index trên UserAccountId để truy vấn nhanh danh sách token của user khi thu hồi
        builder.HasIndex(rt => rt.UserAccountId);

        // Cấu hình Foreign Key với UserAccount
        builder.HasOne(rt => rt.UserAccount)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(rt => rt.UserAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
