using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.System;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AuditLogName).IsRequired().HasMaxLength(150);

        builder.Property(x => x.ActionType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.IPAddress).HasMaxLength(50);
        builder.Property(x => x.EntityName).IsRequired().HasMaxLength(100);

        builder.HasOne(x => x.UserAccount)
               .WithMany()
               .HasForeignKey(x => x.ActorId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
        builder.HasIndex(x => x.CreatedDate);
    }
}
