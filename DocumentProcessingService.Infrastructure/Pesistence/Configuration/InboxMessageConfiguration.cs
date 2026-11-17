using DocumentProcessingService.Infrastructure.Pesistence.Inbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace DocumentProcessingService.Infrastructure.Pesistence.Configuration
{
    public class InboxMessageConfiguration : IEntityTypeConfiguration<InboxMessage>
    {
        public void Configure(EntityTypeBuilder<InboxMessage> builder)
        {
            builder.ToTable("InboxMessages");

            builder.HasKey(it => it.Id);

            builder.Property(x => x.MessageId)
               .IsRequired()
               .HasMaxLength(128);

            builder.Property(x => x.CorrelationId)
                .HasMaxLength(128);

            builder.Property(x => x.Consumer)
                .HasMaxLength(100);

            builder.HasIndex(it => it.MessageId)
                   .IsUnique();

        }
    }
}
