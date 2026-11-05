using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Chat;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Chat;

public class ChatConversationConfiguration : IEntityTypeConfiguration<ChatConversation>
{
    public void Configure(EntityTypeBuilder<ChatConversation> builder)
    {
        builder.ToTable("chat_conversation");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.SupportStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.LastMessagePreview)
            .HasMaxLength(500);

        builder.Property(c => c.LockVersion)
            .IsConcurrencyToken()
            .HasDefaultValue(0);

        builder.HasOne(c => c.InitiatorUser)
            .WithMany()
            .HasForeignKey(c => c.InitiatorUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ParticipantUser)
            .WithMany()
            .HasForeignKey(c => c.ParticipantUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.AssignedStaff)
            .WithMany()
            .HasForeignKey(c => c.AssignedStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(c => c.Messages)
            .WithOne(m => m.Conversation)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes tối ưu hiệu năng truy vấn
        builder.HasIndex(c => new { c.Type, c.InitiatorUserId, c.ParticipantUserId });
        builder.HasIndex(c => new { c.Type, c.SupportStatus, c.AssignedStaffId, c.LastMessageAt });
        builder.HasIndex(c => new { c.InitiatorUserId, c.LastMessageAt });
        builder.HasIndex(c => new { c.ParticipantUserId, c.LastMessageAt });
    }
}
