using DocumentProcessingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocumentProcessingService.Infrastructure.Pesistence.Configuration
{
    public class DocumentConfiguration : IEntityTypeConfiguration<Document>
    {
        public void Configure(EntityTypeBuilder<Document> builder)
        {

           builder.HasKey(e => e.Id);
           builder.Property(e => e.FileName).IsRequired().HasMaxLength(255);
           builder.Property(e => e.Status).IsRequired().HasConversion<string>().
                HasMaxLength(50);
           builder.Property(e => e.CreatedAt).IsRequired();

        }
    }
}
