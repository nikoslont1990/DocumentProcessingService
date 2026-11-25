using DocumentProcessingService.Infrastructure.Pesistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace DocumentProcessingService.Infrastructure.Pesistence.Configuration
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Type).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Content).IsRequired();
            builder.Property(e => e.Status).HasConversion<string>().
                HasMaxLength(50);
            builder.Property(e => e.CreatedAt).IsRequired();
            builder.ToTable("OutboxMessages");
        }
    }
}
