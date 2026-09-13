using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class AiConsultationSessionConfiguration : IEntityTypeConfiguration<AiConsultationSession>
{
    public void Configure(EntityTypeBuilder<AiConsultationSession> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SessionTitle).HasMaxLength(250).IsRequired();
        builder.Property(x => x.TargetBudgetMin).HasPrecision(18, 2);
        builder.Property(x => x.TargetBudgetMax).HasPrecision(18, 2);
        builder.Property(x => x.UserCareer).HasMaxLength(150);
        builder.Property(x => x.FengShuiElement).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserAccountId);
    }
}
