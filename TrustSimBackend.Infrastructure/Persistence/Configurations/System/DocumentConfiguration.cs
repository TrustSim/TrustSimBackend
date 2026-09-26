using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.System;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentName).IsRequired().HasMaxLength(250);
        builder.Property(x => x.LinkUrl).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.DocumentType).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(50).IsRequired();
        builder.Property(x => x.ErrorMessage);

        builder.HasIndex(x => x.DocumentName);
        builder.HasIndex(x => x.DocumentType);
        builder.HasIndex(x => x.Status);
    }
}
