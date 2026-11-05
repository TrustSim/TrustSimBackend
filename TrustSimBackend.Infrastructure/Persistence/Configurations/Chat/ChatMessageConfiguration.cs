using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Chat;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Chat;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_message");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.MessageType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(m => m.AttachmentUrl)
            .HasMaxLength(1000);

        builder.Property(m => m.AttachmentFileName)
            .HasMaxLength(255);

        builder.HasOne(m => m.Sender)
            .WithMany()
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.ConversationId, m.CreatedDate });
        builder.HasIndex(m => new { m.ConversationId, m.IsRead, m.SenderId });
    }
}
