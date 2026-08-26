using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.System;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.System;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DocumentName).IsRequired().HasMaxLength(150);
        builder.Property(x => x.LinkUrl).IsRequired().HasMaxLength(500);

        builder.HasIndex(x => x.DocumentName);
    }
}
